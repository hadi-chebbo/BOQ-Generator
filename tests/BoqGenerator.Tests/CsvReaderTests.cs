namespace BoqGenerator.Tests;

public class CsvReaderTests
{
    [Test]
    public void Finds_a_column_by_name_ignoring_case_and_order()
    {
        var csv = "RATE,Code\n18.50,PB-10\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" });

        Assert.That(result.Rows[0]["rate"], Is.EqualTo("18.50"));
        Assert.That(result.Rows[0]["code"], Is.EqualTo("PB-10"));
    }

    [Test]
    public void Gives_each_row_its_line_number_in_the_file()
    {
        var csv = "Code\nPB-10\nPB-13\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code" });

        Assert.That(result.Rows[0].LineNumber, Is.EqualTo(2));
        Assert.That(result.Rows[1].LineNumber, Is.EqualTo(3));
    }

    [Test]
    public void Ignores_an_invisible_BOM_before_the_first_column_name()
    {
        var csv = "\uFEFFCode,Rate\nPB-10,18.50\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" });

        Assert.That(result.Rows[0]["code"], Is.EqualTo("PB-10"));
    }

    [TestCase("Code,Unit\nPB-10,m2\n", "pricebook.csv is missing the column(s): Rate")]
    [TestCase("Code\nPB-10\n", "pricebook.csv is missing the column(s): Unit, Rate")]
    public void Stops_when_a_required_column_is_missing(string csv, string expectedMessage)
    {
        var error = Assert.Throws<InputFileException>(() =>
            CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Unit", "Rate" }));

        Assert.That(error.Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public void Stops_when_a_required_column_appears_twice()
    {
        var csv = "Code,Rate,rate\nPB-10,18.50,19.00\n";

        var error = Assert.Throws<InputFileException>(() =>
            CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" }));

        Assert.That(error.Message, Is.EqualTo("pricebook.csv has the column(s) more than once: Rate"));
    }

    [Test]
    public void Allows_a_duplicate_column_that_is_not_required()
    {
        var csv = "Code,Notes,Notes\nPB-10,a,b\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code" });

        Assert.That(result.Rows[0]["code"], Is.EqualTo("PB-10"));
    }

    [TestCase("")]
    [TestCase("\n  \n,,,\n")]
    public void Stops_when_the_file_is_empty(string csv)
    {
        var error = Assert.Throws<InputFileException>(() =>
            CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" }));

        Assert.That(error.Message, Is.EqualTo("pricebook.csv is empty"));
    }

    [Test]
    public void Finds_the_header_after_blank_lines_and_keeps_counting_them()
    {
        var csv = "\n,,\nCode\nPB-10\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code" });

        Assert.That(result.Rows[0]["code"], Is.EqualTo("PB-10"));
        Assert.That(result.Rows[0].LineNumber, Is.EqualTo(4));
    }

    [Test]
    public void Stops_when_the_header_has_a_broken_quote()
    {
        var csv = "\nCode,\"Rate\nPB-10,18.50\n";

        var error = Assert.Throws<InputFileException>(() =>
            CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" }));

        Assert.That(error.Message, Is.EqualTo("pricebook.csv: the header on line 2 has a badly quoted column name"));
    }
}
