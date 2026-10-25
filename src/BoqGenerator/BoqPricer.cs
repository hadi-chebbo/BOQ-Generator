namespace BoqGenerator;

// A BOQ line with its quantity with waste, which is the amount that gets priced.
public sealed record PricedLine(BoqLine BoqLine, decimal QuantityWithWaste)
{
    public string Trade => BoqLine.Trade;

    public string Code => BoqLine.Code;
}

public sealed record PricedBoq(IReadOnlyList<PricedLine> Lines, IReadOnlyList<Problem> Problems);

// Works out the money for the BOQ, starting with each line's quantity with waste.
public static class BoqPricer
{
    public static PricedBoq Price(IReadOnlyList<BoqLine> lines, string measurementsFile)
    {
        var pricedLines = new List<PricedLine>();
        var problems = new List<Problem>();

        foreach (var line in lines)
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

            pricedLines.Add(new PricedLine(line, quantityWithWaste));
        }

        return new PricedBoq(pricedLines, problems);
    }

    private static decimal RoundQuantity(decimal unrounded, Unit unit)
    {
        // You can't buy part of a counted item, and the extra one covers breakage: 26.25 downlights is 27.
        if (unit.IsCount)
        {
            return Math.Ceiling(unrounded);
        }

        // Halves round away from zero (283.305 is 283.31), as someone checking by hand expects.
        // C#'s default would round them to even instead.
        return Math.Round(unrounded, 2, MidpointRounding.AwayFromZero);
    }
}
