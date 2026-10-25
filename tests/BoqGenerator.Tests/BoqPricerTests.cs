namespace BoqGenerator.Tests;

public class BoqPricerTests
{
    private const string MeasurementsHeader = "Plan,Trade,Code,Quantity,Unit\n";

    private const string PriceBook =
        "Code,Description,Unit,Rate,WastePercent\n" +
        "PB-10,10mm plasterboard sheet,m2,18.50,10\n" +
        "PT-CEIL,Ceiling paint (2 coats),m2,7.80,\n" +
        "TL-FLR,600x600 porcelain floor tile,m2,64.00,12\n" +
        "CS-DWN,LED downlight,EA,38.00,5\n" +
        "DR-820,820mm hollow-core door,EA,185.00,0\n";

    // Builds the BOQ lines with the real parsers and builder, so the inputs look like the real files.
    private static PricedBoq Price(string measurementRows)
    {
        var measurements = MeasurementParser.Parse(new StringReader(MeasurementsHeader + measurementRows), "measurements.csv");
        var prices = PriceBookParser.Parse(new StringReader(PriceBook), "pricebook.csv");
        var boq = BoqBuilder.Build(measurements.Measurements, prices.PriceBook, "measurements.csv", "pricebook.csv");
        return BoqPricer.Price(boq.Lines);
    }

    [Test]
    public void Adds_waste_to_the_net_quantity_and_rounds_a_half_away_from_zero()
    {
        // 142.6 - 6.4 + 121.35 = 257.55 m2; with 10% waste that's 283.305, which rounds to 283.31.
        var result = Price(
            "Ground Floor,Plasterboard,PB-10,142.6,m2\n" +
            "Ground Floor,Plasterboard,PB-10,-6.4,m2\n" +
            "First Floor,Plasterboard,PB-10,121.35,m2\n");

        Assert.That(result.Lines.Single().QuantityWithWaste, Is.EqualTo(283.31m));
    }

    [TestCase("25", 27)]
    [TestCase("20", 21)]
    [TestCase("2", 3)]
    public void Rounds_a_count_with_waste_up_to_a_whole_item(string quantity, decimal expected)
    {
        var result = Price($"Ground Floor,Electrical,CS-DWN,{quantity},EA\n");

        Assert.That(result.Lines.Single().QuantityWithWaste, Is.EqualTo(expected));
    }

    [TestCase("Ground Floor,Painting,PT-CEIL,179.05,m2\n", 179.05)]
    [TestCase("Ground Floor,Carpentry,DR-820,9,EA\n", 9)]
    [TestCase("Ground Floor,Tiling,TL-FLR,24.6,m2\n", 27.55)]
    [TestCase("Ground Floor,Electrical,CS-DWN,0,EA\n", 0)]
    public void Works_out_the_quantity_with_waste(string row, decimal expected)
    {
        Assert.That(Price(row).Lines.Single().QuantityWithWaste, Is.EqualTo(expected));
    }

    [Test]
    public void Works_out_the_quantities_with_waste_for_the_sample_files()
    {
        var result = BoqPricer.Price(SampleFiles.BuildBoq().Lines);

        Assert.That(result.Lines.Select(line => (line.Code, line.QuantityWithWaste)), Is.EqualTo(new[]
        {
            ("DH-LEV", 9m),
            ("DR-820", 9m),
            ("SK-68", 103.95m),
            ("CS-DWN", 27m),
            ("SB-STONE", 3.93m),
            ("PT-CEIL", 179.05m),
            ("PT-WALL", 476.2m),
            ("PB-10", 283.31m),
            ("PB-13WR", 45.10m),
            ("TL-FLR", 27.55m),
            ("TL-WALL", 35.00m),
        }));
    }
}
