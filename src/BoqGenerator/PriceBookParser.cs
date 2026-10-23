using System.Globalization;

namespace BoqGenerator;

public sealed record PriceBookParseResult(PriceBook PriceBook, IReadOnlyList<Problem> Problems);

public static class PriceBookParser
{
    private static readonly string[] RequiredColumns = { "Code", "Description", "Unit", "Rate", "WastePercent" };

    public static PriceBookParseResult Parse(TextReader text, string fileName)
    {
        var csv = CsvReader.Read(text, fileName, RequiredColumns);
        var items = new List<PriceItem>();
        var problems = new List<Problem>(csv.Problems);

        foreach (var row in csv.Rows)
        {
            var code = row["Code"];
            var rateText = row["Rate"];
            if (!TryParseNumber(rateText, out var rate))
            {
                problems.Add(new Problem(fileName, row.LineNumber, Severity.Error, $"{code}: rate \"{rateText}\" is not a number"));
                continue;
            }

            var wasteText = row["WastePercent"];
            if (!TryParseWastePercent(wasteText, out var wastePercent))
            {
                problems.Add(new Problem(fileName, row.LineNumber, Severity.Error, $"{code}: waste % \"{wasteText}\" is not a number"));
                continue;
            }

            items.Add(new PriceItem(code, row["Description"], row["Unit"], rate, wastePercent, row.LineNumber));
        }

        return new PriceBookParseResult(new PriceBook(items), problems);
    }

    // A blank waste % means no waste.
    private static bool TryParseWastePercent(string text, out decimal wastePercent)
    {
        if (text.Length == 0)
        {
            wastePercent = 0m;
            return true;
        }

        return TryParseNumber(text, out wastePercent);
    }

    // Plain numbers only, like 18.50, for now: ticket 6 adds "$24.80" and "$1,250.00", ticket 7 adds "10%".
    // No thousands separators, so "18,50" can't be read as 1850.
    private static bool TryParseNumber(string text, out decimal number) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out number);
}
