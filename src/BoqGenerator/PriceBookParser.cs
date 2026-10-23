using System.Globalization;
using System.Text.RegularExpressions;

namespace BoqGenerator;

public sealed record PriceBookParseResult(PriceBook PriceBook, IReadOnlyList<Problem> Problems);

public static class PriceBookParser
{
    private static readonly string[] RequiredColumns = { "Code", "Description", "Unit", "Rate", "WastePercent" };

    // A number with an optional $ and optional thousands commas: 18.50, $24.80, $1,250.00.
    // Commas must be in groups of three, so "18,50" isn't read as 1850.
    private static readonly Regex RateFormat = new(@"^-?(\$\s*)?(\d{1,3}(,\d{3})+|\d+)(\.\d+)?$");

    public static PriceBookParseResult Parse(TextReader text, string fileName)
    {
        var csv = CsvReader.Read(text, fileName, RequiredColumns);
        var checkedRows = csv.Rows.Select(row => CheckRow(row, fileName)).ToList();
        var items = new List<PriceItem>();
        var leftOut = new List<LeftOutCode>();
        var problems = new List<Problem>(csv.Problems);

        // Rows with no code can't be copies of anything; they only have their own error.
        problems.AddRange(checkedRows.Where(row => row.Code.Length == 0).SelectMany(row => row.Problems));

        var copiesByCode = checkedRows
            .Where(row => row.Code.Length > 0)
            .GroupBy(row => PriceBook.Key(row.Code));

        foreach (var copies in copiesByCode)
        {
            var decision = DecideCode(copies.ToList(), fileName);
            problems.AddRange(decision.Problems);
            if (decision.Item != null)
            {
                items.Add(decision.Item);
            }
            else
            {
                leftOut.Add(new LeftOutCode(copies.First().Code, copies.Select(copy => copy.Line).ToList()));
            }
        }

        // Grouping by code mixes up the order, so put the problems back in file order.
        var problemsInFileOrder = problems.OrderBy(problem => problem.Line).ToList();
        return new PriceBookParseResult(new PriceBook(items, leftOut), problemsInFileOrder);
    }

    // What happens to one item code: the price item to use (null if the code is left out) and the problems.
    private sealed record CodeDecision(PriceItem? Item, IReadOnlyList<Problem> Problems);

    // Decides an item code from every row that has it. A code is usable only if every copy
    // is valid and they all price the same way.
    private static CodeDecision DecideCode(IReadOnlyList<CheckedRow> copies, string fileName)
    {
        Problem CopyProblem(CheckedRow copy, Severity severity, string message) =>
            new(fileName, copy.Line, severity, $"{copy.Code}: {message}");

        // If any copy can't be used, we can't tell which price is right, so the code is left out.
        var unusable = copies.Where(copy => copy.Item == null).ToList();
        if (unusable.Count > 0)
        {
            var problems = copies.SelectMany(copy => copy.Item == null
                ? copy.Problems
                : new[] { CopyProblem(copy, Severity.Error, $"also on {LinesText(unusable)}, which can't be used; code left out") });
            return new CodeDecision(null, problems.ToList());
        }

        var first = copies[0];
        if (copies.All(copy => HaveSamePrice(copy.Item!, first.Item!)))
        {
            // The repeats add nothing, so only the first copy's own warnings matter.
            var repeatWarnings = copies.Skip(1)
                .Select(repeat => CopyProblem(repeat, Severity.Warning, $"repeats line {first.Line}; line {first.Line} is used"));
            return new CodeDecision(first.Item, first.Problems.Concat(repeatWarnings).ToList());
        }

        var conflicts = copies.Select(copy => CopyProblem(copy, Severity.Error,
            $"also on {LinesText(copies.Where(other => other.Line != copy.Line))} with a different unit, rate or waste %; code left out"));
        return new CodeDecision(null, conflicts.ToList());
    }

    // Copies are the same if they would price the same way. The description doesn't affect any number.
    private static bool HaveSamePrice(PriceItem a, PriceItem b) =>
        string.Equals(a.Unit, b.Unit, StringComparison.OrdinalIgnoreCase)
        && a.Rate == b.Rate
        && a.WastePercent == b.WastePercent;

    // "line 4" or "lines 4, 9".
    private static string LinesText(IEnumerable<CheckedRow> rows)
    {
        var lines = rows.Select(row => row.Line).ToList();
        return (lines.Count == 1 ? "line " : "lines ") + string.Join(", ", lines);
    }

    // One price book row after its values are checked: the price item, or null if the row can't be used,
    // plus the row's own problems.
    private sealed record CheckedRow(string Code, int Line, PriceItem? Item, IReadOnlyList<Problem> Problems);

    private static CheckedRow CheckRow(CsvRow row, string fileName)
    {
        var code = row["Code"];
        if (code.Length == 0)
        {
            return Unusable(new Problem(fileName, row.LineNumber, Severity.Error, "item code is empty"));
        }

        Problem RowProblem(Severity severity, string message) =>
            new(fileName, row.LineNumber, severity, $"{code}: {message}");
        CheckedRow Unusable(Problem error) => new(code, row.LineNumber, null, new[] { error });

        var unit = row["Unit"];
        if (unit.Length == 0)
        {
            return Unusable(RowProblem(Severity.Error, "unit is empty"));
        }

        if (ReadRate(row["Rate"], out var rate) is { } rateError)
        {
            return Unusable(RowProblem(Severity.Error, rateError));
        }

        if (ReadWastePercent(row["WastePercent"], out var wastePercent) is { } wasteError)
        {
            return Unusable(RowProblem(Severity.Error, wasteError));
        }

        // Warnings come after every error check, so an item that is left out only gets its error.
        var warnings = new List<Problem>();
        var description = row["Description"];
        if (description.Length == 0)
        {
            warnings.Add(RowProblem(Severity.Warning, "description is empty"));
        }

        var item = new PriceItem(code, description, unit, rate, wastePercent, row.LineNumber);
        return new CheckedRow(code, row.LineNumber, item, warnings);
    }

    // Returns why the rate can't be used, or null if it can.
    private static string? ReadRate(string text, out decimal rate)
    {
        rate = 0m;
        if (text.Length == 0)
        {
            return "rate is empty";
        }

        var withoutFormatting = text.Replace("$", "").Replace(",", "").Replace(" ", "");
        if (!RateFormat.IsMatch(text) || !TryParseNumber(withoutFormatting, out rate))
        {
            return $"rate \"{text}\" is not a number";
        }

        if (rate < 0)
        {
            return $"rate \"{text}\" is negative";
        }

        // A free item is almost always a rate someone forgot to fill in, so it isn't priced.
        if (rate == 0)
        {
            return "rate is 0";
        }

        return null;
    }

    // Returns why the waste % can't be used, or null if it can. A blank waste % means no waste.
    private static string? ReadWastePercent(string text, out decimal wastePercent)
    {
        wastePercent = 0m;
        if (text.Length == 0)
        {
            return null;
        }

        // "10%" means the same as "10".
        var withoutPercentSign = text.EndsWith('%') ? text[..^1].TrimEnd() : text;
        if (!TryParseNumber(withoutPercentSign, out wastePercent))
        {
            return $"waste % \"{text}\" is not a number";
        }

        if (wastePercent is < 0 or > 100)
        {
            return $"waste % \"{text}\" is not between 0 and 100";
        }

        return null;
    }

    // A plain decimal like 18.50 or -3, with no symbols or separators.
    private static bool TryParseNumber(string text, out decimal number) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out number);
}
