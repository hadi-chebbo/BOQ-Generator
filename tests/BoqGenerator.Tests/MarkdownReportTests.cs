namespace BoqGenerator.Tests;

public class MarkdownReportTests
{
    private const string MeasurementsHeader = "Plan,Trade,Code,Quantity,Unit\n";

    private const string PriceBook =
        "Code,Description,Unit,Rate,WastePercent\n" +
        "PB-10,10mm plasterboard sheet,m2,18.50,10\n" +
        "CS-DWN,LED downlight,EA,38.00,5\n" +
        "SEAL-1,Sealant,m2,18.505,0\n" +
        "PIPE-1,Pipe | 20mm,m,4.00,0\n" +
        "IMG-1,<img src=x> *bold* _it_ `code` & co,EA,1.00,0\n";

    // Prices the measurements with the real pipeline, then writes the Markdown report.
    private static string WriteMarkdown(string measurementRows)
    {
        var generated = BoqGeneration.Generate(
            new StringReader(MeasurementsHeader + measurementRows), "measurements.csv",
            new StringReader(PriceBook), "pricebook.csv");

        var writer = new StringWriter { NewLine = "\n" };
        MarkdownReport.Write(generated.Boq, writer);
        return writer.ToString();
    }

    [Test]
    public void Writes_a_section_per_trade_with_its_lines_and_subtotal_then_the_totals()
    {
        var markdown = WriteMarkdown("Ground Floor,Plasterboard,PB-10,10,m2\n");

        Assert.That(markdown, Is.EqualTo(
            "# Bill of Quantities\n" +
            "\n" +
            "## Plasterboard\n" +
            "\n" +
            "| Code | Description | Quantity | Unit | Rate | Total |\n" +
            "|---|---|---:|---|---:|---:|\n" +
            "| PB-10 | 10mm plasterboard sheet | 11.00 | m2 | 18.50 | 203.50 |\n" +
            "| | **Subtotal** | | | | **203.50** |\n" +
            "\n" +
            "## Totals\n" +
            "\n" +
            "| | Amount |\n" +
            "|---|---:|\n" +
            "| Total excluding GST | 203.50 |\n" +
            "| GST (10%) | 20.35 |\n" +
            "| Total including GST | 223.85 |\n"));
    }

    [Test]
    public void Escapes_a_bar_in_text_so_it_cannot_break_the_table()
    {
        var markdown = WriteMarkdown("Ground Floor,Plumbing,PIPE-1,2,m\n");

        Assert.That(markdown, Does.Contain("| PIPE-1 | Pipe \\| 20mm | 2.00 | m | 4.00 | 8.00 |\n"));
    }

    [Test]
    public void Keeps_every_decimal_of_a_rate()
    {
        var markdown = WriteMarkdown("Ground Floor,Joinery,SEAL-1,5,m2\n");

        Assert.That(markdown, Does.Contain("| SEAL-1 | Sealant | 5.00 | m2 | 18.505 | 92.53 |\n"));
    }

    [Test]
    public void Writes_only_the_totals_for_a_boq_with_no_lines()
    {
        var markdown = WriteMarkdown("Ground Floor,Plumbing,XX-1,2,m\n");

        Assert.That(markdown, Does.StartWith("# Bill of Quantities\n\n## Totals\n"));
        Assert.That(markdown, Does.Contain("| Total including GST | 0.00 |\n"));
    }

    [Test]
    public void Shows_html_and_markdown_in_text_as_plain_text()
    {
        var markdown = WriteMarkdown("Ground Floor,<b>Fit-out</b>,IMG-1,1,EA\n");

        Assert.That(markdown, Does.Contain("## &lt;b&gt;Fit-out&lt;/b&gt;\n"));
        Assert.That(markdown, Does.Contain(
            "| IMG-1 | &lt;img src=x&gt; \\*bold\\* \\_it\\_ \\`code\\` &amp; co | 1.00 | EA | 1.00 | 1.00 |\n"));
    }
}
