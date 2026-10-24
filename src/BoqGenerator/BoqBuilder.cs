namespace BoqGenerator;

public sealed record BoqBuildResult(IReadOnlyList<BoqLine> Lines, IReadOnlyList<Problem> Problems);

public static class BoqBuilder
{
    public static BoqBuildResult Build(
        IReadOnlyList<Measurement> measurements, PriceBook priceBook, string measurementsFile, string priceBookFile)
    {
        var priced = new List<(Measurement Measurement, PriceItem PriceItem)>();
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

            // For now the units must be the same. Ticket 20 adds converting mm, cm and m.
            if (measurement.Unit != priceItem.Unit)
            {
                problems.Add(MeasurementError($"measured in {measurement.Unit.Name} but priced per {priceItem.Unit.Name}"));
                continue;
            }

            priced.Add((measurement, priceItem));
        }

        // Each trade is shown the way it is first written, so all its lines use one spelling.
        var tradeNames = new Dictionary<string, string>();
        foreach (var (measurement, _) in priced)
        {
            tradeNames.TryAdd(MatchKey.For(measurement.Trade), measurement.Trade);
        }

        // One BOQ line per trade and item code, across every plan.
        var lines = priced
            .GroupBy(pair => (Trade: MatchKey.For(pair.Measurement.Trade), Code: PriceBook.Key(pair.PriceItem.Code)))
            .OrderBy(group => group.Key.Trade, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Code, StringComparer.Ordinal)
            .Select(group => new BoqLine(
                tradeNames[group.Key.Trade],
                group.First().PriceItem,
                group.Sum(pair => pair.Measurement.Quantity),
                group.Select(pair => pair.Measurement.Line).ToList()))
            .ToList();

        var problemsInFileOrder = problems.OrderBy(problem => problem.Line).ToList();
        return new BoqBuildResult(lines, problemsInFileOrder);
    }
}
