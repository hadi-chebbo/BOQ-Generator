namespace BoqGenerator;

// A BOQ line with its quantity with waste, which is the amount that gets priced.
public sealed record PricedLine(BoqLine BoqLine, decimal QuantityWithWaste)
{
    public string Trade => BoqLine.Trade;

    public string Code => BoqLine.Code;
}

public sealed record PricedBoq(IReadOnlyList<PricedLine> Lines);

// Works out the money for the BOQ, starting with each line's quantity with waste.
public static class BoqPricer
{
    public static PricedBoq Price(IReadOnlyList<BoqLine> lines) =>
        new(lines.Select(line => new PricedLine(line, QuantityWithWaste(line))).ToList());

    // Waste goes on the line's net quantity (after deductions).
    private static decimal QuantityWithWaste(BoqLine line)
    {
        var withWaste = line.Quantity * (1 + line.PriceItem.WastePercent / 100);

        // You can't buy part of a counted item, and the extra one covers breakage: 26.25 downlights is 27.
        if (line.Unit.IsCount)
        {
            return Math.Ceiling(withWaste);
        }

        // Halves round away from zero (283.305 is 283.31), as someone checking by hand expects.
        // C#'s default would round them to even instead.
        return Math.Round(withWaste, 2, MidpointRounding.AwayFromZero);
    }
}
