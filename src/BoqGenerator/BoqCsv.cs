namespace BoqGenerator;

// Writes the BOQ as CSV: a row per BOQ line, and a subtotal row after each trade's lines.
// The overall totals aren't in the file; they're printed on the console.
public static class BoqCsv
{
    // A comma, quote or line break would split a cell.
    private static readonly char[] CharactersNeedingQuotes = { ',', '"', '\r', '\n' };

    // Excel runs a cell starting with one of these as a formula.
    private const string FormulaStarts = "=+-@\t\r";

    public static void Write(PricedBoq boq, TextWriter writer)
    {
        WriteRow(writer, "Trade", "Code", "Description", "Quantity", "Unit", "Rate", "Total");

        foreach (var trade in boq.Trades)
        {
            foreach (var line in trade.Lines)
            {
                WriteRow(writer,
                    trade.Trade,
                    line.Code,
                    line.Description,
                    NumberText.TwoDecimals(line.QuantityWithWaste),
                    line.Unit.Name,
                    NumberText.Rate(line.Rate),
                    NumberText.TwoDecimals(line.LineTotal));
            }

            // Every column blank except the trade name (in Description) and its subtotal.
            WriteRow(writer, "", "", trade.Trade, "", "", "", NumberText.TwoDecimals(trade.Subtotal));
        }
    }

    // Every BOQ row has these 7 columns. The text columns are made safe here, so no row can forget it;
    // the numbers are written by the program and never need it. Rows end in CRLF, the standard CSV line ending.
    private static void WriteRow(
        TextWriter writer, string trade, string code, string description, string quantity, string unit, string rate, string total)
    {
        var cells = new[] { SafeText(trade), SafeText(code), SafeText(description), quantity, SafeText(unit), rate, total };
        writer.Write(string.Join(",", cells) + "\r\n");
    }

    // Text from the input files, made safe for a CSV cell that Excel opens.
    private static string SafeText(string value)
    {
        // A price book could make Excel run anything, so a leading ' makes Excel show it as plain text instead.
        if (value.Length > 0 && FormulaStarts.Contains(value[0]))
        {
            value = "'" + value;
        }

        // Wrap the cell in quotes and double any quote inside, so it stays one cell.
        if (value.IndexOfAny(CharactersNeedingQuotes) >= 0)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
