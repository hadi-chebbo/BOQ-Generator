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

    [TestCase("abc")]
    [TestCase("$5")]
    [TestCase(".5")]
    [TestCase("18,50")]
    public void Leaves_out_and_reports_a_row_whose_quantity_is_not_a_number(string quantityText)
    {
        var result = Parse(Header + $"First Floor,Tiling,TL-FLR,\"{quantityText}\",m2\nFirst Floor,Tiling,TL-WALL,18.7,m2\n");

        Assert.That(result.Measurements.Select(measurement => measurement.Code), Is.EqualTo(new[] { "TL-WALL" }));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, $"TL-FLR: quantity \"{quantityText}\" is not a number"),
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
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 4, Severity.Warning, "PB-10: quantity -6.4 is a deduction"),
            new Problem("measurements.csv", 15, Severity.Error, "DR-820: quantity is empty"),
            new Problem("measurements.csv", 17, Severity.Warning, "CS-DWN: quantity is 0"),
            new Problem("measurements.csv", 22, Severity.Error, "TL-FLR: quantity \"abc\" is not a number"),
        }));
    }

    [Test]
    public void Leaves_out_and_reports_a_row_with_no_quantity()
    {
        var result = Parse(Header + "First Floor,Carpentry,DR-820,,EA\n");

        Assert.That(result.Measurements, Is.Empty);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "DR-820: quantity is empty"),
        }));
    }

    [TestCase("0")]
    [TestCase("0.00")]
    [TestCase("-0")]
    public void Keeps_but_warns_about_a_quantity_of_zero(string quantityText)
    {
        var result = Parse(Header + $"Ground Floor,Electrical,CS-DWN,{quantityText},EA\n");

        Assert.That(result.Measurements.Single().Quantity, Is.EqualTo(0m));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Warning, "CS-DWN: quantity is 0"),
        }));
    }

    [TestCase("-6.4", -6.4)]
    [TestCase("-1,250.5", -1250.5)]
    public void Keeps_a_negative_quantity_as_a_deduction_and_warns_about_it(string quantityText, decimal expected)
    {
        var result = Parse(Header + $"Ground Floor,Plasterboard,PB-10,\"{quantityText}\",m2\n");

        Assert.That(result.Measurements.Single().Quantity, Is.EqualTo(expected));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Warning, $"PB-10: quantity {quantityText} is a deduction"),
        }));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Leaves_out_and_reports_a_row_with_no_code(string code)
    {
        var result = Parse(Header + $"Ground Floor,Plasterboard,{code},142.6,m2\n");

        Assert.That(result.Measurements, Is.Empty);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "item code is empty"),
        }));
    }

    [TestCase("Ground Floor,,PB-10,142.6,m2")]
    [TestCase("Ground Floor,   ,PB-10,142.6,m2")]
    [TestCase("Ground Floor,,PB-10,abc,")]
    public void Leaves_out_and_reports_a_row_with_no_trade(string row)
    {
        var result = Parse(Header + row + "\n");

        Assert.That(result.Measurements, Is.Empty);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "PB-10: trade is empty"),
        }));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Leaves_out_and_reports_a_row_with_no_unit(string unit)
    {
        var result = Parse(Header + $"First Floor,Plasterboard,CN-90,41750,{unit}\n");

        Assert.That(result.Measurements, Is.Empty);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "CN-90: unit is empty"),
        }));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Keeps_but_warns_about_a_row_with_no_plan(string plan)
    {
        var result = Parse(Header + $"{plan},Plasterboard,PB-10,142.6,m2\n");

        Assert.That(result.Measurements, Is.EqualTo(new[]
        {
            new Measurement("", "Plasterboard", "PB-10", 142.6m, "m2", Line: 2),
        }));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Warning, "PB-10: plan is empty"),
        }));
    }

    [Test]
    public void Reports_only_the_error_when_a_row_with_no_plan_is_left_out()
    {
        var result = Parse(Header + ",Plasterboard,PB-10,-6.4,\n");

        Assert.That(result.Measurements, Is.Empty);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "PB-10: unit is empty"),
        }));
    }

    [Test]
    public void Reports_every_warning_on_a_kept_row_in_a_fixed_order()
    {
        var result = Parse(Header + ",Electrical,CS-DWN,0,EA\n");

        Assert.That(result.Measurements, Has.Count.EqualTo(1));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Warning, "CS-DWN: plan is empty"),
            new Problem("measurements.csv", 2, Severity.Warning, "CS-DWN: quantity is 0"),
        }));
    }
}
