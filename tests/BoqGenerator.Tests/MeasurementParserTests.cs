namespace BoqGenerator.Tests;

public class MeasurementParserTests
{
    private const string Header = "Plan,Trade,Code,Quantity,Unit\n";

    private static MeasurementParseResult Parse(string csv) =>
        MeasurementParser.Parse(new StringReader(csv), "measurements.csv");

    [Test]
    public void Turns_a_clean_row_into_a_measurement()
    {
        var result = Parse(Header + "Ground Floor,Plasterboard,PB-10,142.6,m2\n");

        Assert.That(result.Measurements, Is.EqualTo(new[]
        {
            new Measurement("Ground Floor", "Plasterboard", "PB-10", 142.6m, "m2", Line: 2),
        }));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Leaves_out_and_reports_a_row_whose_quantity_is_not_a_number()
    {
        var result = Parse(Header + "First Floor,Tiling,TL-FLR,abc,m2\nFirst Floor,Tiling,TL-WALL,18.7,m2\n");

        Assert.That(result.Measurements.Select(measurement => measurement.Code), Is.EqualTo(new[] { "TL-WALL" }));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "TL-FLR: quantity \"abc\" is not a number"),
        }));
    }

    [Test]
    public void Lists_the_problems_from_reading_and_parsing_in_line_order()
    {
        var result = Parse(Header + "First Floor,Tiling,TL-FLR,abc,m2\nFirst Floor,\"Tiling,TL-WALL,18.7,m2\n");

        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "TL-FLR: quantity \"abc\" is not a number"),
            new Problem("measurements.csv", 3, Severity.Error, "has a badly quoted value"),
        }));
    }

    [Test]
    public void Keeps_case_and_spelling_and_rows_in_file_order()
    {
        var result = Parse(Header + "Ground Floor,Plasterboard,PB-10,-6.4,m2\nFirst Floor,plasterboard ,pb-13wr,\"1,250.5\",M2\n");

        Assert.That(result.Measurements, Is.EqualTo(new[]
        {
            new Measurement("Ground Floor", "Plasterboard", "PB-10", -6.4m, "m2", Line: 2),
            new Measurement("First Floor", "plasterboard", "pb-13wr", 1250.5m, "M2", Line: 3),
        }));
    }

    [Test]
    public void Stops_when_a_measurement_column_is_missing()
    {
        var error = Assert.Throws<InputFileException>(() => Parse("Plan,Trade,Code,Quantity\nGround Floor,Plasterboard,PB-10,142.6\n"));

        Assert.That(error.Message, Is.EqualTo("measurements.csv is missing the column(s): Unit"));
    }

    [Test]
    public void Reads_the_sample_measurements_without_crashing()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "measurements.csv");
        using var file = File.OpenText(path);

        var result = MeasurementParser.Parse(file, "measurements.csv");

        // 27 data rows, minus line 15 (no quantity) and line 22 ("abc").
        Assert.That(result.Measurements, Has.Count.EqualTo(25));
        Assert.That(result.Problems.Select(problem => problem.Line), Is.EqualTo(new[] { 15, 22 }));
    }
}
