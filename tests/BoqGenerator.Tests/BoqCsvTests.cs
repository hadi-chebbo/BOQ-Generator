namespace BoqGenerator.Tests;

public class BoqCsvTests
{
    private const string Header = "Trade,Code,Description,Quantity,Unit,Rate,Total\r\n";
    private const string MeasurementsHeader = "Plan,Trade,Code,Quantity,Unit\n";

    private const string PriceBook =
        "Code,Description,Unit,Rate,WastePercent\n" +
        "PB-10,10mm plasterboard sheet,m2,18.50,10\n" +
        "PB-13WR,13mm wet-area plasterboard,m2,24.80,10\n" +
        "CS-DWN,LED downlight,EA,38.00,5\n" +
        "SEAL-1,Sealant,m2,18.505,0\n" +
        "DR-1,\"Door, 820mm\",EA,185.00,0\n" +
        "BD-1,\"Board \"\"A\"\"\",m2,10.00,0\n" +
        "EVIL-1,=HYPERLINK(\"http://x\"),EA,1.00,0\n" +
        "AT-1,@SUM(A1),EA,1.00,0\n" +
        "+X1,Odd code,-,2.00,0\n";

    // Prices the measurements with the real parsers, builder and pricer, then writes the CSV.
    private static string WriteCsv(string measurementRows)
    {
        var measurements = MeasurementParser.Parse(new StringReader(MeasurementsHeader + measurementRows), "measurements.csv");
        var prices = PriceBookParser.Parse(new StringReader(PriceBook), "pricebook.csv");
        var boq = BoqBuilder.Build(measurements.Measurements, prices.PriceBook, "measurements.csv", "pricebook.csv");
        var priced = BoqPricer.Price(boq.Lines, "measurements.csv");

        var writer = new StringWriter();
        BoqCsv.Write(priced, writer);
        return writer.ToString();
    }

    [Test]
    public void Writes_the_header_a_row_per_line_and_a_subtotal_row_for_its_trade()
    {
        var csv = WriteCsv("Ground Floor,Plasterboard,PB-10,10,m2\n");

        Assert.That(csv, Is.EqualTo(
            Header +
            "Plasterboard,PB-10,10mm plasterboard sheet,11.00,m2,18.50,203.50\r\n" +
            ",,Plasterboard,,,,203.50\r\n"));
    }

    [Test]
    public void Keeps_every_decimal_of_a_rate_so_quantity_times_rate_still_equals_the_total()
    {
        var csv = WriteCsv("Ground Floor,Joinery,SEAL-1,5,m2\n");

        Assert.That(csv, Does.Contain("Joinery,SEAL-1,Sealant,5.00,m2,18.505,92.53\r\n"));
    }

    [Test]
    public void Writes_a_count_with_two_decimals_like_every_other_quantity()
    {
        var csv = WriteCsv("Ground Floor,Electrical,CS-DWN,25,EA\n");

        Assert.That(csv, Does.Contain("Electrical,CS-DWN,LED downlight,27.00,EA,38.00,1026.00\r\n"));
    }

    [Test]
    public void Puts_each_trade_subtotal_row_straight_after_that_trade_lines()
    {
        var csv = WriteCsv(
            "Ground Floor,Plasterboard,PB-10,10,m2\n" +
            "Ground Floor,Electrical,CS-DWN,2,EA\n" +
            "Ground Floor,Plasterboard,PB-13WR,10,m2\n");

        Assert.That(csv, Is.EqualTo(
            Header +
            "Electrical,CS-DWN,LED downlight,3.00,EA,38.00,114.00\r\n" +
            ",,Electrical,,,,114.00\r\n" +
            "Plasterboard,PB-10,10mm plasterboard sheet,11.00,m2,18.50,203.50\r\n" +
            "Plasterboard,PB-13WR,13mm wet-area plasterboard,11.00,m2,24.80,272.80\r\n" +
            ",,Plasterboard,,,,476.30\r\n"));
    }

    [Test]
    [SetCulture("de-DE")]
    public void Writes_numbers_with_a_point_whatever_the_computer_language_is()
    {
        var csv = WriteCsv("Ground Floor,Joinery,SEAL-1,5,m2\n");

        Assert.That(csv, Does.Contain("5.00,m2,18.505,92.53\r\n"));
    }

    [Test]
    public void Writes_the_boq_for_the_sample_files()
    {
        var writer = new StringWriter();
        BoqCsv.Write(SampleFiles.PriceBoq(), writer);

        Assert.That(writer.ToString(), Is.EqualTo(
            Header +
            "Carpentry,DH-LEV,Lever door handle set,9.00,EA,42.00,378.00\r\n" +
            "Carpentry,DR-820,820mm hollow-core door,9.00,EA,185.00,1665.00\r\n" +
            "Carpentry,SK-68,68mm pine skirting,103.95,m,9.45,982.33\r\n" +
            ",,Carpentry,,,,3025.33\r\n" +
            "Electrical,CS-DWN,LED downlight,27.00,EA,38.00,1026.00\r\n" +
            ",,Electrical,,,,1026.00\r\n" +
            "Joinery,SB-STONE,20mm stone benchtop,3.93,m2,1250.00,4912.50\r\n" +
            ",,Joinery,,,,4912.50\r\n" +
            "Painting,PT-CEIL,Ceiling paint (2 coats),179.05,m2,7.80,1396.59\r\n" +
            "Painting,PT-WALL,Wall paint (2 coats),476.20,m2,8.40,4000.08\r\n" +
            ",,Painting,,,,5396.67\r\n" +
            "Plasterboard,PB-10,10mm plasterboard sheet,283.31,m2,18.50,5241.24\r\n" +
            "Plasterboard,PB-13WR,13mm wet-area plasterboard,45.10,m2,24.80,1118.48\r\n" +
            ",,Plasterboard,,,,6359.72\r\n" +
            "Tiling,TL-FLR,600x600 porcelain floor tile,27.55,m2,64.00,1763.20\r\n" +
            "Tiling,TL-WALL,300x600 ceramic wall tile,35.00,m2,58.50,2047.50\r\n" +
            ",,Tiling,,,,3810.70\r\n"));
    }

    [Test]
    public void Quotes_text_that_contains_a_comma_so_the_columns_stay_in_place()
    {
        var csv = WriteCsv("Ground Floor,Carpentry,DR-1,2,EA\n");

        Assert.That(csv, Does.Contain("Carpentry,DR-1,\"Door, 820mm\",2.00,EA,185.00,370.00\r\n"));
    }

    [Test]
    public void Doubles_a_quote_inside_quoted_text()
    {
        var csv = WriteCsv("Ground Floor,Plasterboard,BD-1,1,m2\n");

        Assert.That(csv, Does.Contain("Plasterboard,BD-1,\"Board \"\"A\"\"\",1.00,m2,10.00,10.00\r\n"));
    }

    [Test]
    public void Stops_excel_running_text_that_starts_like_a_formula()
    {
        var csv = WriteCsv(
            "Ground Floor,Carpentry,EVIL-1,1,EA\n" +
            "Ground Floor,Carpentry,AT-1,1,EA\n" +
            "Ground Floor,-Demolition,PB-10,10,m2\n");

        Assert.That(csv, Does.Contain("Carpentry,AT-1,'@SUM(A1),1.00,EA,1.00,1.00\r\n"));
        Assert.That(csv, Does.Contain("Carpentry,EVIL-1,\"'=HYPERLINK(\"\"http://x\"\")\",1.00,EA,1.00,1.00\r\n"));
        Assert.That(csv, Does.Contain("'-Demolition,PB-10,10mm plasterboard sheet,11.00,m2,18.50,203.50\r\n"));
        Assert.That(csv, Does.Contain(",,'-Demolition,,,,203.50\r\n"));
    }

    [Test]
    public void Guards_the_code_and_unit_columns_too()
    {
        var csv = WriteCsv("Ground Floor,Joinery,+X1,3,-\n");

        Assert.That(csv, Does.Contain("Joinery,'+X1,Odd code,3.00,'-,2.00,6.00\r\n"));
    }
}
