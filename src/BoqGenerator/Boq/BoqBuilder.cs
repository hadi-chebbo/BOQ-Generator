namespace BoqGenerator;

public sealed record BoqBuildResult(IReadOnlyList<BoqLine> Lines, IReadOnlyList<Problem> Problems);

// Turns measurements and the price book into BOQ lines, in four steps: price each measurement,
// combine them by trade and item code, check each line's total, and list the problems in file order.
public static class BoqBuilder
{
    public static BoqBuildResult Build(
        IReadOnlyList<Measurement> measurements, PriceBook priceBook, string measurementsFile, string priceBookFile)
    {
        var problems = new List<Problem>();
        var priced = Price(measurements, priceBook, measurementsFile, priceBookFile, problems);
        var combined = Combine(priced);
        var kept = CheckTotals(combined, measurementsFile, problems);

        var problemsInFileOrder = problems.OrderBy(problem => problem.Line).ToList();
        return new BoqBuildResult(kept, problemsInFileOrder);
    }

    // Pairs each measurement with its price item, in the price item's unit. A measurement that can't be
    // priced is reported and left out.
    private static List<PricedMeasurement> Price(
        IReadOnlyList<Measurement> measurements, PriceBook priceBook, string measurementsFile, string priceBookFile,
        List<Problem> problems)
    {
        var priced = new List<PricedMeasurement>();
        foreach (var measurement in measurements)
        {
            Problem MeasurementError(string message) =>
                new(measurementsFile, measurement.Line, Severity.Error, $"{measurement.Code}: {message}");

            if (!priceBook.TryFind(measurement.Code, out var priceItem))
            {
                // A code the price book left out already has its own problem there, so point to it.
                problems.Add(priceBook.TryFindLeftOut(measurement.Code, out var priceBookLines)
                    ? MeasurementError($"no usable price (see {priceBookFile} {LineNumbers.Describe(priceBookLines)})")
                    : MeasurementError("not in the price book"));
                continue;
            }

            // A safe conversion (mm, cm, m) is silent; anything else, like m against m2, can't be priced.
            if (!Unit.TryConvert(measurement.Quantity, measurement.Unit, priceItem.Unit, out var quantity))
            {
                problems.Add(MeasurementError($"measured in {measurement.Unit.Name} but priced per {priceItem.Unit.Name}"));
                continue;
            }

            priced.Add(new PricedMeasurement(measurement, priceItem, quantity));
        }

        return priced;
    }

    // One BOQ line per trade and item code, across every plan, sorted by trade and then code.
    private static List<BoqLine> Combine(List<PricedMeasurement> priced)
    {
        // Each trade is shown the way it is first written, so all its lines use one spelling.
        var tradeNames = new Dictionary<string, string>();
        foreach (var each in priced)
        {
            tradeNames.TryAdd(MatchKey.For(each.Measurement.Trade), each.Measurement.Trade);
        }

        return priced
            .GroupBy(each => (Trade: MatchKey.For(each.Measurement.Trade), Code: MatchKey.For(each.PriceItem.Code)))
            .OrderBy(group => group.Key.Trade, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Code, StringComparer.Ordinal)
            .Select(group => new BoqLine(
                tradeNames[group.Key.Trade],
                group.First().PriceItem,
                group.Sum(each => each.PricedQuantity),
                group.Select(each => each.Measurement.Line).ToList()))
            .ToList();
    }

    // Leaves out lines whose total is below zero and warns about lines whose total is 0.
    private static List<BoqLine> CheckTotals(List<BoqLine> combined, string measurementsFile, List<Problem> problems)
    {
        var kept = new List<BoqLine>();
        foreach (var boqLine in combined)
        {
            Problem LineProblem(Severity severity, string message) => boqLine.Problem(measurementsFile, severity, message);

            // More deducted than measured: a negative amount of material makes no sense.
            if (boqLine.Quantity < 0)
            {
                problems.Add(LineProblem(Severity.Error,
                    $"total {NumberText.Plain(boqLine.Quantity)} {boqLine.Unit.Name} is below zero " +
                    $"({LineNumbers.Describe(boqLine.MeasurementLines)}); line left out"));
                continue;
            }

            // Kept so the BOQ still shows the item was measured, but someone should check it.
            if (boqLine.Quantity == 0)
            {
                problems.Add(LineProblem(Severity.Warning, $"total is 0 ({LineNumbers.Describe(boqLine.MeasurementLines)})"));
            }

            kept.Add(boqLine);
        }

        return kept;
    }

    // A measurement paired with its price item. PricedQuantity is the measured quantity converted to the
    // price item's unit; it's the one to add up, not Measurement.Quantity.
    private sealed record PricedMeasurement(Measurement Measurement, PriceItem PriceItem, decimal PricedQuantity);
}
