namespace BoqGenerator;

// Writes the BOQ as CSV: a row per BOQ line, and a subtotal row after each trade's lines.
// The overall totals aren't in the file; they're printed on the console.
public static class BoqCsv
{
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

    // CSV rows end in CRLF, the standard CSV line ending.
    private static void WriteRow(TextWriter writer, params string[] fields) =>
        writer.Write(string.Join(",", fields) + "\r\n");
}
