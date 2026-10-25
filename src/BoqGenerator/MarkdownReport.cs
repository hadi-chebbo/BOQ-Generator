namespace BoqGenerator;

// Writes the BOQ as a Markdown report: a section per trade with a table of its lines and its subtotal,
// then a summary of the totals.
public static class MarkdownReport
{
    public static void Write(PricedBoq boq, TextWriter writer)
    {
        writer.WriteLine("# Bill of Quantities");

        foreach (var trade in boq.Trades)
        {
            writer.WriteLine();
            writer.WriteLine($"## {Text(trade.Trade)}");
            writer.WriteLine();
            writer.WriteLine("| Code | Description | Quantity | Unit | Rate | Total |");
            writer.WriteLine("|---|---|---:|---|---:|---:|");
            foreach (var line in trade.Lines)
            {
                writer.WriteLine(
                    $"| {Text(line.Code)} | {Text(line.Description)} | {NumberText.TwoDecimals(line.QuantityWithWaste)} " +
                    $"| {Text(line.Unit.Name)} | {NumberText.Rate(line.Rate)} | {NumberText.TwoDecimals(line.LineTotal)} |");
            }

            writer.WriteLine($"| | **Subtotal** | | | | **{NumberText.TwoDecimals(trade.Subtotal)}** |");
        }

        writer.WriteLine();
        writer.WriteLine("## Totals");
        writer.WriteLine();
        writer.WriteLine("| | Amount |");
        writer.WriteLine("|---|---:|");
        foreach (var (label, amount) in boq.Totals)
        {
            writer.WriteLine($"| {label} | {NumberText.TwoDecimals(amount)} |");
        }
    }

    // Text from the input files, shown exactly as written. HTML (<img>, <details>) would otherwise render or hide
    // part of the page, and Markdown (*, _, `) would change the text; a "|" would end a table cell early.
    private static string Text(string text)
    {
        var safe = text
            .Replace("\\", "\\\\")
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        foreach (var special in new[] { "|", "*", "_", "`" })
        {
            safe = safe.Replace(special, "\\" + special);
        }

        return safe;
    }
}
