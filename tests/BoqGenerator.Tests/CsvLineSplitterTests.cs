namespace BoqGenerator.Tests;

public class CsvLineSplitterTests
{
    [Test]
    public void Splits_a_plain_line_on_commas()
    {
        var result = CsvLineSplitter.Split("PB-10,10mm plasterboard sheet,m2,18.50");

        Assert.That(result.Fields, Is.EqualTo(new[] { "PB-10", "10mm plasterboard sheet", "m2", "18.50" }));
    }

    [Test]
    public void Keeps_a_comma_that_is_inside_quotes()
    {
        var result = CsvLineSplitter.Split("SB-STONE,20mm stone benchtop,m2,\"$1,250.00\",15");

        Assert.That(result.Fields, Is.EqualTo(new[] { "SB-STONE", "20mm stone benchtop", "m2", "$1,250.00", "15" }));
    }

    [Test]
    public void Turns_a_doubled_quote_inside_quotes_into_one_quote()
    {
        var result = CsvLineSplitter.Split("\"Board \"\"A\"\"\",x");

        Assert.That(result.Fields, Is.EqualTo(new[] { "Board \"A\"", "x" }));
    }

    [TestCase(" PB-10 , m2 ")]
    [TestCase("\" PB-10 \",\" m2 \"")]
    public void Trims_spaces_around_each_field_quoted_or_not(string line)
    {
        var result = CsvLineSplitter.Split(line);

        Assert.That(result.Fields, Is.EqualTo(new[] { "PB-10", "m2" }));
    }

    [TestCase("a,,b", new[] { "a", "", "b" })]
    [TestCase("a,b,", new[] { "a", "b", "" })]
    [TestCase(",a,b", new[] { "", "a", "b" })]
    public void Keeps_empty_fields_in_place(string line, string[] expected)
    {
        var result = CsvLineSplitter.Split(line);

        Assert.That(result.Fields, Is.EqualTo(expected));
    }

    [Test]
    public void Reports_a_line_whose_quote_is_never_closed()
    {
        var result = CsvLineSplitter.Split("PB-13WR,\"$24.80,m2");

        Assert.That(result.HasUnclosedQuote, Is.True);
        Assert.That(result.Fields, Is.Empty);
    }
}
