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

    [Test]
    public void Reports_no_problems_for_good_rows()
    {
        var csv = "Code,Rate\nPB-10,18.50\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" });

        Assert.That(result.Rows, Has.Count.EqualTo(1));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Skips_and_reports_a_row_with_a_broken_quote()
    {
        var csv = "Code,Rate\nPB-10,\"18.50\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" });

        Assert.That(result.Rows, Is.Empty);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "has a badly quoted value"),
        }));
    }

    [TestCase("PB-10,18.50,10\n")]
    [TestCase("PB-10,18.50,10,,\n")]
    public void Skips_and_reports_a_row_with_more_values_than_columns(string row)
    {
        var csv = "Code,Rate\n" + row;

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" });

        Assert.That(result.Rows, Is.Empty);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "has 3 values but the header has 2 columns"),
        }));
    }

    [Test]
    public void Keeps_a_row_whose_extra_values_are_all_empty()
    {
        var csv = "Code,Rate\nPB-10,18.50,,\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" });

        Assert.That(result.Rows[0]["rate"], Is.EqualTo("18.50"));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Treats_missing_values_at_the_end_of_a_short_row_as_empty()
    {
        var csv = "Plan,Code,Quantity,Unit\nFirst Floor,DR-820\n";

        var result = CsvReader.Read(new StringReader(csv), "measurements.csv", new[] { "Code", "Quantity", "Unit" });

        Assert.That(result.Rows[0]["code"], Is.EqualTo("DR-820"));
        Assert.That(result.Rows[0]["quantity"], Is.EqualTo(""));
        Assert.That(result.Rows[0]["unit"], Is.EqualTo(""));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Skips_blank_lines_between_rows_without_reporting_them()
    {
        var csv = "Code,Rate\nPB-10,18.50\n\n , ,\nPB-13,24.80\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" });

        Assert.That(result.Rows.Select(row => row.LineNumber), Is.EqualTo(new[] { 2, 5 }));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Keeps_reading_after_a_bad_row()
    {
        var csv = "Code,Rate\nPB-10,\"18.50\nSB-STONE,$1,250.00\nPB-13,24.80\n";

        var result = CsvReader.Read(new StringReader(csv), "pricebook.csv", new[] { "Code", "Rate" });

        Assert.That(result.Rows.Select(row => row["code"]), Is.EqualTo(new[] { "PB-13" }));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "has a badly quoted value"),
            new Problem("pricebook.csv", 3, Severity.Error, "has 3 values but the header has 2 columns"),
        }));
    }
}
