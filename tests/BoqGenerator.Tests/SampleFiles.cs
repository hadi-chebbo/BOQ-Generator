namespace BoqGenerator.Tests;

// Builds the BOQ lines from the sample files in data/, the same way the program will.
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

    public static PricedBoq PriceBoq() => BoqPricer.Price(BuildBoq().Lines, "measurements.csv");
}
