namespace BoqGenerator.Tests;

// The sample files in data/: the BOQ lines on their own (for the builder tests), or the whole pipeline.
internal static class SampleFiles
{
    public static BoqBuildResult BuildBoq()
    {
        var data = Path.Combine(TestContext.CurrentContext.TestDirectory, "data");
        using var measurementsFile = File.OpenText(Path.Combine(data, "measurements.csv"));
        using var priceBookFile = File.OpenText(Path.Combine(data, "pricebook.csv"));
        var measurements = MeasurementParser.Parse(measurementsFile, "measurements.csv");
        var prices = PriceBookParser.Parse(priceBookFile, "pricebook.csv");
        return BoqBuilder.Build(measurements.Measurements, prices.PriceBook, "measurements.csv", "pricebook.csv");
    }

    // The whole pipeline on the sample files, exactly as the program runs it.
    public static GeneratedBoq Generate()
    {
        var data = Path.Combine(TestContext.CurrentContext.TestDirectory, "data");
        using var measurementsFile = File.OpenText(Path.Combine(data, "measurements.csv"));
        using var priceBookFile = File.OpenText(Path.Combine(data, "pricebook.csv"));
        return BoqGeneration.Generate(measurementsFile, "measurements.csv", priceBookFile, "pricebook.csv");
    }

    public static PricedBoq PriceBoq() => Generate().Boq;
}
