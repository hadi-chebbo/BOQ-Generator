namespace BoqGenerator;

public sealed record BoqBuildResult(IReadOnlyList<BoqLine> Lines, IReadOnlyList<Problem> Problems);

public static class BoqBuilder
{
    public static BoqBuildResult Build(
        IReadOnlyList<Measurement> measurements, PriceBook priceBook, string measurementsFile, string priceBookFile)
    {
        var priced = new List<PricedMeasurement>();
        var problems = new List<Problem>();
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

        // Each trade is shown the way it is first written, so all its lines use one spelling.
        var tradeNames = new Dictionary<string, string>();
        foreach (var each in priced)
        {
            tradeNames.TryAdd(MatchKey.For(each.Measurement.Trade), each.Measurement.Trade);
        }

        // One BOQ line per trade and item code, across every plan.
        var lines = priced
            .GroupBy(each => (Trade: MatchKey.For(each.Measurement.Trade), Code: PriceBook.Key(each.PriceItem.Code)))
            .OrderBy(group => group.Key.Trade, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Code, StringComparer.Ordinal)
            .Select(group => new BoqLine(
                tradeNames[group.Key.Trade],
                group.First().PriceItem,
                group.Sum(each => each.PricedQuantity),
                group.Select(each => each.Measurement.Line).ToList()))
            .ToList();

        var problemsInFileOrder = problems.OrderBy(problem => problem.Line).ToList();
        return new BoqBuildResult(lines, problemsInFileOrder);
    }

    // A measurement paired with its price item. PricedQuantity is the measured quantity converted to the
    // price item's unit; it's the one to add up, not Measurement.Quantity.
    private sealed record PricedMeasurement(Measurement Measurement, PriceItem PriceItem, decimal PricedQuantity);
}
