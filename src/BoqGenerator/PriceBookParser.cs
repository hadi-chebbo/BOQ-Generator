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
        var items = new List<PriceItem>();
        var problems = new List<Problem>(csv.Problems);

        foreach (var row in csv.Rows)
        {
            var code = row["Code"];
            if (code.Length == 0)
            {
                problems.Add(new Problem(fileName, row.LineNumber, Severity.Error, "item code is empty"));
                continue;
            }

            Problem RowProblem(Severity severity, string message) =>
                new(fileName, row.LineNumber, severity, $"{code}: {message}");

            var unit = row["Unit"];
            if (unit.Length == 0)
            {
                problems.Add(RowProblem(Severity.Error, "unit is empty"));
                continue;
            }

            if (ReadRate(row["Rate"], out var rate) is { } rateError)
            {
                problems.Add(RowProblem(Severity.Error, rateError));
                continue;
            }

            if (ReadWastePercent(row["WastePercent"], out var wastePercent) is { } wasteError)
            {
                problems.Add(RowProblem(Severity.Error, wasteError));
                continue;
            }

            // Warnings come after every error check, so an item that is left out only gets its error.
            if (rate == 0)
            {
                problems.Add(RowProblem(Severity.Warning, "rate is 0"));
            }

            var description = row["Description"];
            if (description.Length == 0)
            {
                problems.Add(RowProblem(Severity.Warning, "description is empty"));
            }

            items.Add(new PriceItem(code, description, unit, rate, wastePercent, row.LineNumber));
        }

        return new PriceBookParseResult(new PriceBook(items), problems);
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
