namespace BoqGenerator;

// A BOQ line with its money worked out: the quantity with waste, which is what gets priced, and the line total.
public sealed record PricedLine(BoqLine BoqLine, decimal QuantityWithWaste, decimal LineTotal)
{
    public string Trade => BoqLine.Trade;

    public string Code => BoqLine.Code;

    public string Description => BoqLine.Description;

    public Unit Unit => BoqLine.Unit;

    public decimal Rate => BoqLine.PriceItem.Rate;
}

// One trade's priced lines, in BOQ order, and their subtotal.
public sealed record PricedTrade(string Trade, IReadOnlyList<PricedLine> Lines)
{
    // The sum of the rounded line totals, so the printed BOQ adds up exactly. Excludes GST.
    public decimal Subtotal => Lines.Sum(line => line.LineTotal);
}

// The whole BOQ priced, trade by trade. The totals are worked out from the trades, so they always agree.
public sealed record PricedBoq(IReadOnlyList<PricedTrade> Trades, IReadOnlyList<Problem> Problems)
{
    // Goods and Services Tax, added to the total excluding GST.
    private const decimal GstRate = 0.10m;

    public IReadOnlyList<PricedLine> Lines => Trades.SelectMany(trade => trade.Lines).ToList();

    public decimal TotalExcludingGst => Trades.Sum(trade => trade.Subtotal);

    public decimal Gst => Rounding.ToHundredths(TotalExcludingGst * GstRate);

    public decimal TotalIncludingGst => TotalExcludingGst + Gst;
}

// Works out the money for the BOQ: each line's quantity with waste and line total, grouped into trades.
public static class BoqPricer
{
    public static PricedBoq Price(IReadOnlyList<BoqLine> lines, string measurementsFile)
    {
        var problems = new List<Problem>();
        var pricedLines = lines.Select(line => PriceLine(line, measurementsFile, problems)).ToList();

        // Lines are already sorted by trade, and every line of a trade shows the same spelling of it.
        var trades = pricedLines
            .GroupBy(line => MatchKey.For(line.Trade))
            .Select(trade => new PricedTrade(trade.First().Trade, trade.ToList()))
            .ToList();

        return new PricedBoq(trades, problems);
    }

    private static PricedLine PriceLine(BoqLine line, string measurementsFile, List<Problem> problems)
    {
        // Waste goes on the line's net quantity (after deductions).
        var unrounded = line.Quantity * (1 + line.PriceItem.WastePercent / 100);
        var quantityWithWaste = RoundQuantity(unrounded, line.Unit);

        // A real quantity that rounds away to nothing would be priced at $0 without anyone knowing.
        if (unrounded != 0 && quantityWithWaste == 0)
        {
            problems.Add(line.Problem(measurementsFile, Severity.Warning,
                $"{NumberText.Plain(unrounded)} {line.Unit.Name} rounds to 0.00"));
        }

        // The rate is used exactly as written; only the result is rounded, to the nearest cent.
        var lineTotal = Rounding.ToHundredths(quantityWithWaste * line.PriceItem.Rate);

        return new PricedLine(line, quantityWithWaste, lineTotal);
    }

    private static decimal RoundQuantity(decimal unrounded, Unit unit)
    {
        // You can't buy part of a counted item, and the extra one covers breakage: 26.25 downlights is 27.
        if (unit.IsCount)
        {
            return Math.Ceiling(unrounded);
        }

        return Rounding.ToHundredths(unrounded);
    }
}
