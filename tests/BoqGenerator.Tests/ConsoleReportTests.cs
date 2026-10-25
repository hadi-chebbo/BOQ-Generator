namespace BoqGenerator.Tests;

public class ConsoleReportTests
{
    private const string MeasurementsHeader = "Plan,Trade,Code,Quantity,Unit\n";

    private const string PriceBook =
        "Code,Description,Unit,Rate,WastePercent\n" +
        "PB-10,10mm plasterboard sheet,m2,18.50,10\n" +
        "CS-DWN,LED downlight,EA,38.00,5\n";

    // Prices the measurements with the real pipeline, then writes the report with every problem found.
    private static string Report(string measurementRows, string priceBook = PriceBook)
    {
        var generated = BoqGeneration.Generate(
            new StringReader(MeasurementsHeader + measurementRows), "measurements.csv",
            new StringReader(priceBook), "pricebook.csv");

        var writer = new StringWriter { NewLine = "\n" };
        ConsoleReport.Write(generated.Problems, "pricebook.csv", generated.Boq, "boq.csv", writer);
        return writer.ToString();
    }

    [Test]
    public void Reports_no_problems_then_the_subtotals_totals_and_where_the_boq_went()
    {
        var report = Report("Ground Floor,Plasterboard,PB-10,10,m2\n");

        Assert.That(report, Is.EqualTo(
            "Problems: none\n" +
            "\n" +
            "Trade subtotals (excluding GST):\n" +
            "  Plasterboard       203.50\n" +
            "\n" +
            "Total excluding GST  203.50\n" +
            "GST (10%)             20.35\n" +
            "Total including GST  223.85\n" +
            "\n" +
            "BOQ written to boq.csv (1 line).\n"));
    }

    [Test]
    public void Lists_the_price_book_problems_first_then_the_rest_in_line_order()
    {
        var report = Report(
            "Ground Floor,Plasterboard,PB-10,10,m2\n" +
            "Ground Floor,Carpentry,AR-66,1,m\n" +
            "Ground Floor,Carpentry,XX-1,1,m\n" +
            "Ground Floor,Electrical,CS-DWN,0,EA\n",
            PriceBook + "AR-66,66mm pine architrave,m,TBC,5\n");

        Assert.That(report, Does.StartWith(
            "Problems: 3 errors, 2 warnings\n" +
            "  pricebook.csv:4     Error    AR-66: rate \"TBC\" is not a number\n" +
            "  measurements.csv:3  Error    AR-66: no usable price (see pricebook.csv line 4)\n" +
            "  measurements.csv:4  Error    XX-1: not in the price book\n" +
            "  measurements.csv:5  Warning  CS-DWN: quantity is 0\n" +
            "  measurements.csv:5  Warning  CS-DWN in Electrical: total is 0\n" +
            "\n"));
    }

    [Test]
    public void Uses_the_singular_for_one_problem()
    {
        var report = Report("Ground Floor,Carpentry,XX-1,1,m\n");

        Assert.That(report, Does.StartWith("Problems: 1 error, 0 warnings\n"));
    }

    [Test]
    public void Reports_a_boq_with_no_lines_without_crashing()
    {
        var report = Report("Ground Floor,Carpentry,XX-1,1,m\n");

        Assert.That(report, Does.Contain("Trade subtotals (excluding GST):\n  none\n"));
        Assert.That(report, Does.EndWith("BOQ written to boq.csv (0 lines).\n"));
    }

    [Test]
    public void Writes_the_report_for_the_sample_files()
    {
        var generated = SampleFiles.Generate();

        var writer = new StringWriter { NewLine = "\n" };
        ConsoleReport.Write(generated.Problems, "pricebook.csv", generated.Boq, "boq.csv", writer);

        Assert.That(writer.ToString(), Is.EqualTo(
            "Problems: 10 errors, 2 warnings\n" +
            "  pricebook.csv:4      Error    CN-90: also on line 15 with a different unit, rate or waste %; code left out\n" +
            "  pricebook.csv:6      Error    AR-66: rate \"TBC\" is not a number\n" +
            "  pricebook.csv:15     Error    CN-90: also on line 4 with a different unit, rate or waste %; code left out\n" +
            "  measurements.csv:4   Warning  PB-10: quantity -6.4 is a deduction\n" +
            "  measurements.csv:5   Error    CN-90: no usable price (see pricebook.csv lines 4, 15)\n" +
            "  measurements.csv:8   Error    CN-90: no usable price (see pricebook.csv lines 4, 15)\n" +
            "  measurements.csv:11  Error    AR-66: no usable price (see pricebook.csv line 6)\n" +
            "  measurements.csv:15  Error    DR-820: quantity is empty\n" +
            "  measurements.csv:17  Warning  CS-DWN: quantity is 0\n" +
            "  measurements.csv:21  Error    TL-WALL: measured in m but priced per m2\n" +
            "  measurements.csv:22  Error    TL-FLR: quantity \"abc\" is not a number\n" +
            "  measurements.csv:29  Error    PT-TRIM: not in the price book\n" +
            "\n" +
            "Trade subtotals (excluding GST):\n" +
            "  Carpentry           3025.33\n" +
            "  Electrical          1026.00\n" +
            "  Joinery             4912.50\n" +
            "  Painting            5396.67\n" +
            "  Plasterboard        6359.72\n" +
            "  Tiling              3810.70\n" +
            "\n" +
            "Total excluding GST  24530.92\n" +
            "GST (10%)             2453.09\n" +
            "Total including GST  26984.01\n" +
            "\n" +
            "BOQ written to boq.csv (11 lines).\n"));
    }

    [Test]
    public void Groups_problems_from_any_other_file_after_the_price_book()
    {
        var problems = new[]
        {
            new Problem("measurements.csv", 3, Severity.Error, "B"),
            new Problem("other.csv", 2, Severity.Error, "C"),
            new Problem("measurements.csv", 5, Severity.Error, "D"),
            new Problem("pricebook.csv", 9, Severity.Error, "A"),
        };
        var writer = new StringWriter { NewLine = "\n" };

        ConsoleReport.Write(problems, "pricebook.csv", new PricedBoq(Array.Empty<PricedTrade>(), problems), "boq.csv", writer);

        var messages = writer.ToString().Split('\n').Skip(1).Take(4).Select(line => line[^1..]);
        Assert.That(messages, Is.EqualTo(new[] { "A", "B", "D", "C" }));
    }
}
