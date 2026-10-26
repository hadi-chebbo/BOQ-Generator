namespace BoqGenerator;

// The priced BOQ and every problem found on the way, from both files.
public sealed record GeneratedBoq(PricedBoq Boq, IReadOnlyList<Problem> Problems);

// The whole pipeline: read both files, build the BOQ lines, and price them.
public static class BoqGeneration
{
    // Throws InputFileException if a file can't be used at all (a missing column, an empty file).
    public static GeneratedBoq Generate(
        TextReader measurementsText, string measurementsFile, TextReader priceBookText, string priceBookFile)
    {
        var prices = PriceBookParser.Parse(priceBookText, priceBookFile);
        var measurements = MeasurementParser.Parse(measurementsText, measurementsFile);
        var lines = BoqBuilder.Build(measurements.Measurements, prices.PriceBook, measurementsFile, priceBookFile);
        var priced = BoqPricer.Price(lines.Lines, measurementsFile);

        var problems = prices.Problems
            .Concat(measurements.Problems)
            .Concat(lines.Problems)
            .Concat(priced.Problems)
            .ToList();
        return new GeneratedBoq(priced.Boq, problems);
    }
}
