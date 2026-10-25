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
        "DR-820,820mm hollow-core door,EA,185.00,0\n" +
        "SK-68,68mm pine skirting,m,9.45,5\n" +
        "SEAL-1,Sealant (rate with 3 decimals),m2,18.505,0\n" +
        "FIX-1,Fixing kit,EA,12.25,0\n";

    // Builds the BOQ lines with the real parsers and builder, so the inputs look like the real files.
    private static PricedBoq Price(string measurementRows)
    {
        var measurements = MeasurementParser.Parse(new StringReader(MeasurementsHeader + measurementRows), "measurements.csv");
        var prices = PriceBookParser.Parse(new StringReader(PriceBook), "pricebook.csv");
        var boq = BoqBuilder.Build(measurements.Measurements, prices.PriceBook, "measurements.csv", "pricebook.csv");
        return BoqPricer.Price(boq.Lines, "measurements.csv");
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
        var result = BoqPricer.Price(SampleFiles.BuildBoq().Lines, "measurements.csv");

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

    [Test]
    public void Warns_when_a_quantity_with_waste_rounds_to_zero()
    {
        // 3 mm is 0.003 m; with 5% waste that's 0.00315 m, which rounds to 0.00.
        var result = Price("Ground Floor,Carpentry,SK-68,3,mm\n");

        Assert.That(result.Lines.Single().QuantityWithWaste, Is.EqualTo(0m));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("measurements.csv", 2, Severity.Warning, "SK-68 in Carpentry: 0.00315 m rounds to 0.00"),
        }));
    }

    [Test]
    public void Adds_no_warning_for_a_line_that_was_already_zero()
    {
        var result = Price("Ground Floor,Electrical,CS-DWN,0,EA\n");

        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Works_out_the_line_total_from_the_quantity_with_waste()
    {
        // 283.31 m2 x 18.50 = 5241.235, which rounds to 5241.24.
        var result = Price(
            "Ground Floor,Plasterboard,PB-10,142.6,m2\n" +
            "Ground Floor,Plasterboard,PB-10,-6.4,m2\n" +
            "First Floor,Plasterboard,PB-10,121.35,m2\n");

        Assert.That(result.Lines.Single().LineTotal, Is.EqualTo(5241.24m));
    }

    [Test]
    public void Uses_the_rate_as_written_and_rounds_a_half_cent_away_from_zero()
    {
        // 5 m2 x 18.505 = 92.525: away from zero is 92.53 (rounding to even would give 92.52).
        var result = Price("Ground Floor,Joinery,SEAL-1,5,m2\n");

        Assert.That(result.Lines.Single().LineTotal, Is.EqualTo(92.53m));
    }

    [TestCase("Ground Floor,Electrical,CS-DWN,25,EA\n", 1026.00)]
    [TestCase("Ground Floor,Electrical,CS-DWN,0,EA\n", 0)]
    public void Works_out_the_line_total_for_a_count(string row, decimal expected)
    {
        Assert.That(Price(row).Lines.Single().LineTotal, Is.EqualTo(expected));
    }

    [Test]
    public void Works_out_the_line_totals_for_the_sample_files()
    {
        var result = BoqPricer.Price(SampleFiles.BuildBoq().Lines, "measurements.csv");

        Assert.That(result.Lines.Select(line => (line.Code, line.LineTotal)), Is.EqualTo(new[]
        {
            ("DH-LEV", 378.00m),
            ("DR-820", 1665.00m),
            ("SK-68", 982.33m),
            ("CS-DWN", 1026.00m),
            ("SB-STONE", 4912.50m),
            ("PT-CEIL", 1396.59m),
            ("PT-WALL", 4000.08m),
            ("PB-10", 5241.24m),
            ("PB-13WR", 1118.48m),
            ("TL-FLR", 1763.20m),
            ("TL-WALL", 2047.50m),
        }));
    }

    [Test]
    public void Adds_up_the_line_totals_of_each_trade()
    {
        var result = Price(
            "Ground Floor,Plasterboard,PB-10,10,m2\n" +
            "Ground Floor,Electrical,CS-DWN,2,EA\n" +
            "Ground Floor,Tiling,TL-FLR,24.6,m2\n" +
            "Ground Floor,Electrical,DR-820,1,EA\n");

        // Electrical: 3 x 38.00 + 1 x 185.00; Plasterboard: 11 x 18.50; Tiling: 27.55 x 64.00.
        Assert.That(result.Trades.Select(trade => (trade.Trade, trade.Subtotal)), Is.EqualTo(new[]
        {
            ("Electrical", 299.00m),
            ("Plasterboard", 203.50m),
            ("Tiling", 1763.20m),
        }));
    }

    [Test]
    public void Adds_gst_to_the_total_and_rounds_a_half_cent_away_from_zero()
    {
        // 12.25 excluding GST; GST is 1.225, which rounds to 1.23 (rounding to even would give 1.22).
        var result = Price("Ground Floor,Joinery,FIX-1,1,EA\n");

        Assert.That(result.TotalExcludingGst, Is.EqualTo(12.25m));
        Assert.That(result.Gst, Is.EqualTo(1.23m));
        Assert.That(result.TotalIncludingGst, Is.EqualTo(13.48m));
    }

    [Test]
    public void Works_out_the_subtotals_and_totals_for_the_sample_files()
    {
        var result = BoqPricer.Price(SampleFiles.BuildBoq().Lines, "measurements.csv");

        Assert.That(result.Trades.Select(trade => (trade.Trade, trade.Subtotal)), Is.EqualTo(new[]
        {
            ("Carpentry", 3025.33m),
            ("Electrical", 1026.00m),
            ("Joinery", 4912.50m),
            ("Painting", 5396.67m),
            ("Plasterboard", 6359.72m),
            ("Tiling", 3810.70m),
        }));
        Assert.That(result.TotalExcludingGst, Is.EqualTo(24530.92m));
        Assert.That(result.Gst, Is.EqualTo(2453.09m));
        Assert.That(result.TotalIncludingGst, Is.EqualTo(26984.01m));
    }

    [Test]
    public void Prices_an_empty_boq_at_zero()
    {
        var result = BoqPricer.Price(Array.Empty<BoqLine>(), "measurements.csv");

        Assert.That(result.Trades, Is.Empty);
        Assert.That(result.TotalExcludingGst, Is.EqualTo(0m));
        Assert.That(result.Gst, Is.EqualTo(0m));
        Assert.That(result.TotalIncludingGst, Is.EqualTo(0m));
    }
}
