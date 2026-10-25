namespace BoqGenerator;

// A BOQ line with its money worked out: the quantity with waste, which is what gets priced, and the line total.
public sealed record PricedLine(BoqLine BoqLine, decimal QuantityWithWaste, decimal LineTotal)
{
    public string Trade => BoqLine.Trade;

    public string Code => BoqLine.Code;
}

public sealed record PricedBoq(IReadOnlyList<PricedLine> Lines, IReadOnlyList<Problem> Problems);

// Works out the money for the BOQ: each line's quantity with waste and line total.
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

            // The rate is used exactly as written; only the result is rounded, to the nearest cent.
            var lineTotal = RoundToHundredths(quantityWithWaste * line.PriceItem.Rate);

            pricedLines.Add(new PricedLine(line, quantityWithWaste, lineTotal));
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

        return RoundToHundredths(unrounded);
    }

    // Every quantity and amount of money is rounded the same way: to 2 decimals, with halves away from zero
    // (283.305 is 283.31), as someone checking by hand expects. C#'s default would round halves to even.
    private static decimal RoundToHundredths(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
