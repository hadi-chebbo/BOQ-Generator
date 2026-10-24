namespace BoqGenerator.Tests;

public class BoqBuilderTests
{
    private const string MeasurementsHeader = "Plan,Trade,Code,Quantity,Unit\n";
    private const string PriceBookHeader = "Code,Description,Unit,Rate,WastePercent\n";

    private const string PriceBook =
        PriceBookHeader +
        "PB-10,10mm plasterboard sheet,m2,18.50,10\n" +
        "PB-13WR,13mm wet-area plasterboard,m2,24.80,10\n" +
        "SK-68,68mm pine skirting,m,9.45,5\n";

    private static BoqBuildResult Build(string measurementRows, string priceBook = PriceBook) =>
        Build(new StringReader(MeasurementsHeader + measurementRows), new StringReader(priceBook));

    // Reads both files with the real parsers, so the inputs look like the real files.
    private static BoqBuildResult Build(TextReader measurementsText, TextReader priceBookText)
    {
        var measurements = MeasurementParser.Parse(measurementsText, "measurements.csv");
        var prices = PriceBookParser.Parse(priceBookText, "pricebook.csv");
        return BoqBuilder.Build(measurements.Measurements, prices.PriceBook, "measurements.csv", "pricebook.csv");
    }

    [Test]
    public void Turns_a_measurement_with_a_matching_price_item_into_a_boq_line()
    {
        var result = Build("Ground Floor,Plasterboard,PB-10,142.6,m2\n");

        var line = result.Lines.Single();
        Assert.That(line.Trade, Is.EqualTo("Plasterboard"));
        Assert.That(line.Code, Is.EqualTo("PB-10"));
        Assert.That(line.Description, Is.EqualTo("10mm plasterboard sheet"));
        Assert.That(line.Unit, Is.EqualTo(Unit.Read("m2")));
        Assert.That(line.Quantity, Is.EqualTo(142.6m));
        Assert.That(line.PriceItem.Rate, Is.EqualTo(18.50m));
        Assert.That(line.MeasurementLines, Is.EqualTo(new[] { 2 }));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Combines_the_same_item_in_the_same_trade_across_all_plans()
    {
        var result = Build(
            "Ground Floor,Plasterboard,PB-13WR,18.2,m2\n" +
            "First Floor,plasterboard ,pb-13wr,22.8,M2\n");

        var line = result.Lines.Single();
        Assert.That(line.Trade, Is.EqualTo("Plasterboard"));
        Assert.That(line.Code, Is.EqualTo("PB-13WR"));
        Assert.That(line.Quantity, Is.EqualTo(41.0m));
        Assert.That(line.MeasurementLines, Is.EqualTo(new[] { 2, 3 }));
    }

    [Test]
    public void Subtracts_a_deduction_when_combining()
    {
        var result = Build(
            "Ground Floor,Plasterboard,PB-10,142.6,m2\n" +
            "Ground Floor,Plasterboard,PB-10,-6.4,m2\n" +
            "First Floor,Plasterboard,PB-10,121.35,m2\n");

        Assert.That(result.Lines.Single().Quantity, Is.EqualTo(257.55m));
    }

    [Test]
    public void Keeps_the_same_item_in_two_trades_as_two_lines()
    {
        var result = Build(
            "Ground Floor,Plasterboard,PB-10,100,m2\n" +
            "Ground Floor,Joinery,PB-10,5,m2\n");

        Assert.That(result.Lines.Select(line => (line.Trade, line.Quantity)), Is.EqualTo(new[]
        {
            ("Joinery", 5m),
            ("Plasterboard", 100m),
        }));
    }

    [Test]
    public void Sorts_lines_by_trade_and_then_by_code()
    {
        var result = Build(
            "Ground Floor,Plasterboard,PB-13WR,1,m2\n" +
            "Ground Floor,carpentry,SK-68,1,m\n" +
            "Ground Floor,Plasterboard,PB-10,1,m2\n");

        Assert.That(result.Lines.Select(line => $"{line.Trade} {line.Code}"), Is.EqualTo(new[]
        {
            "carpentry SK-68",
            "Plasterboard PB-10",
            "Plasterboard PB-13WR",
        }));
    }

    [Test]
    public void Leaves_out_and_reports_a_measurement_whose_code_is_not_in_the_price_book()
    {
        var result = Build(
            "First Floor,Painting,PT-TRIM,40,m\n" +
            "Ground Floor,Plasterboard,PB-10,142.6,m2\n");

        Assert.That(result.Lines.Select(line => line.Code), Is.EqualTo(new[] { "PB-10" }));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "PT-TRIM: not in the price book"),
        }));
    }

    [Test]
    public void Leaves_out_and_reports_a_measurement_in_a_different_unit_from_its_price()
    {
        var result = Build(
            "Ground Floor,Plasterboard,PB-10,20,m2\n" +
            "First Floor,Plasterboard,PB-10,18.7,m\n");

        Assert.That(result.Lines.Single().Quantity, Is.EqualTo(20m));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 3, Severity.Error, "PB-10: measured in m but priced per m2"),
        }));
    }

    [Test]
    public void Builds_the_boq_lines_for_the_sample_files()
    {
        var data = Path.Combine(TestContext.CurrentContext.TestDirectory, "data");
        using var measurementsFile = File.OpenText(Path.Combine(data, "measurements.csv"));
        using var priceBookFile = File.OpenText(Path.Combine(data, "pricebook.csv"));

        var result = Build(measurementsFile, priceBookFile);

        Assert.That(result.Lines.Select(line => FormattableString.Invariant($"{line.Trade} {line.Code} {line.Quantity} {line.Unit.Name}")), Is.EqualTo(new[]
        {
            "Carpentry DH-LEV 9 EA",
            "Carpentry DR-820 9 EA",
            "Carpentry SK-68 99.0 m",
            "Electrical CS-DWN 25 EA",
            "Joinery SB-STONE 3.42 m2",
            "Painting PT-CEIL 179.05 m2",
            "Painting PT-WALL 476.2 m2",
            "Plasterboard PB-10 257.55 m2",
            "Plasterboard PB-13WR 41.0 m2",
            "Tiling TL-FLR 24.6 m2",
            "Tiling TL-WALL 31.25 m2",
        }));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 5, Severity.Error, "CN-90: no usable price (see pricebook.csv lines 4, 15)"),
            new Problem("measurements.csv", 8, Severity.Error, "CN-90: no usable price (see pricebook.csv lines 4, 15)"),
            new Problem("measurements.csv", 11, Severity.Error, "AR-66: no usable price (see pricebook.csv line 6)"),
            new Problem("measurements.csv", 21, Severity.Error, "TL-WALL: measured in m but priced per m2"),
            new Problem("measurements.csv", 29, Severity.Error, "PT-TRIM: not in the price book"),
        }));
    }

    [Test]
    public void Shows_one_spelling_for_a_trade_across_all_its_lines()
    {
        var result = Build(
            "Ground Floor,carpentry,SK-68,10,m\n" +
            "Ground Floor,Plasterboard,PB-10,1,m2\n" +
            "First Floor,Carpentry,PB-13WR,2,m2\n");

        Assert.That(result.Lines.Select(line => $"{line.Trade} {line.Code}"), Is.EqualTo(new[]
        {
            "carpentry PB-13WR",
            "carpentry SK-68",
            "Plasterboard PB-10",
        }));
    }

    [Test]
    public void Points_to_the_price_book_when_a_code_was_left_out_of_it()
    {
        var result = Build(
            "Ground Floor,Carpentry,AR-66,62.4,m\n",
            PriceBookHeader + "AR-66,66mm pine architrave,m,TBC,5\n");

        Assert.That(result.Lines, Is.Empty);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "AR-66: no usable price (see pricebook.csv line 2)"),
        }));
    }

    [Test]
    public void Names_every_price_book_line_of_a_duplicate_code_that_was_left_out()
    {
        var result = Build(
            "Ground Floor,Plasterboard,CN-90,48.2,m\n",
            PriceBookHeader + "CN-90,90mm cove cornice,m,6.20,5\nCN-90,90mm cove cornice,m,6.75,5\n");

        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Error, "CN-90: no usable price (see pricebook.csv lines 2, 3)"),
        }));
    }

    [Test]
    public void Converts_a_measurement_to_the_unit_of_its_price()
    {
        var result = Build("First Floor,Carpentry,SK-68,41750,mm\n");

        var line = result.Lines.Single();
        Assert.That(line.Unit, Is.EqualTo(Unit.Read("m")));
        Assert.That(line.Quantity, Is.EqualTo(41.75m));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Combines_measurements_taken_in_different_length_units()
    {
        var result = Build(
            "Ground Floor,Carpentry,SK-68,48.2,m\n" +
            "First Floor,Carpentry,SK-68,41750,mm\n" +
            "First Floor,Carpentry,SK-68,250,cm\n");

        var line = result.Lines.Single();
        Assert.That(line.Quantity, Is.EqualTo(92.45m));
        Assert.That(line.MeasurementLines, Is.EqualTo(new[] { 2, 3, 4 }));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Converts_metres_to_a_price_per_millimetre()
    {
        var result = Build(
            "Ground Floor,Joinery,SEAL-1,2.5,m\n",
            PriceBookHeader + "SEAL-1,Silicone bead,mm,0.02,0\n");

        Assert.That(result.Lines.Single().Quantity, Is.EqualTo(2500m));
    }
}
