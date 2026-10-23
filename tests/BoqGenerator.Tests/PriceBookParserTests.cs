namespace BoqGenerator.Tests;

public class PriceBookParserTests
{
    private const string Header = "Code,Description,Unit,Rate,WastePercent\n";

    private static PriceBookParseResult Parse(string csv) =>
        PriceBookParser.Parse(new StringReader(csv), "pricebook.csv");

    [Test]
    public void Turns_a_clean_row_into_a_price_item()
    {
        var result = Parse(Header + "PB-10,10mm plasterboard sheet,m2,18.50,10\n");

        Assert.That(result.PriceBook.TryFind("PB-10", out var item), Is.True);
        Assert.That(item, Is.EqualTo(new PriceItem("PB-10", "10mm plasterboard sheet", "m2", 18.50m, 10m, Line: 2)));
        Assert.That(result.Problems, Is.Empty);
    }

    [TestCase("PB-13WR")]
    [TestCase("pb-13wr")]
    [TestCase("  Pb-13Wr  ")]
    public void Finds_a_price_item_ignoring_case_and_surrounding_spaces(string code)
    {
        var result = Parse(Header + "PB-13WR,13mm wet-area plasterboard,m2,24.80,10\n");

        Assert.That(result.PriceBook.TryFind(code, out var item), Is.True);
        Assert.That(item.Code, Is.EqualTo("PB-13WR"));
    }

    [Test]
    public void Treats_a_blank_waste_percent_as_no_waste()
    {
        var result = Parse(Header + "DH-LEV,Lever door handle set,EA,42.00,\n");

        result.PriceBook.TryFind("DH-LEV", out var item);
        Assert.That(item.WastePercent, Is.EqualTo(0m));
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Leaves_out_and_reports_an_item_whose_rate_is_not_a_number()
    {
        var result = Parse(Header + "AR-66,66mm pine architrave,m,TBC,5\nSK-68,68mm pine skirting,m,9.45,5\n");

        Assert.That(result.PriceBook.TryFind("AR-66", out _), Is.False);
        Assert.That(result.PriceBook.TryFind("SK-68", out _), Is.True);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "AR-66: rate \"TBC\" is not a number"),
        }));
    }

    [Test]
    public void Keeps_the_first_copy_of_a_code_that_appears_twice()
    {
        var result = Parse(Header + "CN-90,90mm cove cornice,m,6.20,5\ncn-90,90mm cove cornice,m,6.75,5\n");

        result.PriceBook.TryFind("CN-90", out var item);
        Assert.That(item.Rate, Is.EqualTo(6.20m));
    }

    [Test]
    public void Passes_on_the_problems_found_while_reading_the_file()
    {
        var result = Parse(Header + "PB-10,\"10mm plasterboard,m2,18.50,10\n");

        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "has a badly quoted value"),
        }));
    }

    [Test]
    public void Stops_when_a_price_book_column_is_missing()
    {
        var error = Assert.Throws<InputFileException>(() => Parse("Code,Description,Unit,Rate\nPB-10,Sheet,m2,18.50\n"));

        Assert.That(error.Message, Is.EqualTo("pricebook.csv is missing the column(s): WastePercent"));
    }

    [Test]
    public void Reads_the_sample_price_book_without_crashing()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "pricebook.csv");
        using var file = File.OpenText(path);

        var result = PriceBookParser.Parse(file, "pricebook.csv");

        Assert.That(result.PriceBook.TryFind("PB-10", out var item), Is.True);
        Assert.That(item.Rate, Is.EqualTo(18.50m));
    }

    [TestCase("24.80", 24.80)]
    [TestCase("$24.80", 24.80)]
    [TestCase("$1,250.00", 1250.00)]
    [TestCase("1,250.00", 1250.00)]
    [TestCase("$12,500", 12500)]
    [TestCase("$ 24.80", 24.80)]
    [TestCase("18.505", 18.505)]
    public void Reads_a_rate_written_with_a_dollar_sign_or_thousands_commas(string rateText, decimal expected)
    {
        var result = Parse(Header + $"SB-STONE,20mm stone benchtop,m2,\"{rateText}\",15\n");

        result.PriceBook.TryFind("SB-STONE", out var item);
        Assert.That(item.Rate, Is.EqualTo(expected));
        Assert.That(result.Problems, Is.Empty);
    }

    [TestCase("18,50")]
    [TestCase("AUD 18")]
    [TestCase("1,25,000")]
    [TestCase("$")]
    [TestCase("18.50.1")]
    public void Leaves_out_and_reports_a_rate_that_is_not_a_number(string rateText)
    {
        var result = Parse(Header + $"CN-90,90mm cove cornice,m,\"{rateText}\",5\n");

        Assert.That(result.PriceBook.TryFind("CN-90", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, $"CN-90: rate \"{rateText}\" is not a number"),
        }));
    }

    [Test]
    public void Leaves_out_and_reports_an_empty_rate()
    {
        var result = Parse(Header + "CN-90,90mm cove cornice,m,,5\n");

        Assert.That(result.PriceBook.TryFind("CN-90", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "CN-90: rate is empty"),
        }));
    }

    [TestCase("-6.20")]
    [TestCase("-$6.20")]
    public void Leaves_out_and_reports_a_negative_rate(string rateText)
    {
        var result = Parse(Header + $"CN-90,90mm cove cornice,m,{rateText},5\n");

        Assert.That(result.PriceBook.TryFind("CN-90", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, $"CN-90: rate \"{rateText}\" is negative"),
        }));
    }

    [TestCase("0")]
    [TestCase("$0.00")]
    public void Keeps_but_warns_about_a_rate_of_zero(string rateText)
    {
        var result = Parse(Header + $"PT-WALL,Wall paint (2 coats),m2,{rateText},\n");

        Assert.That(result.PriceBook.TryFind("PT-WALL", out var item), Is.True);
        Assert.That(item.Rate, Is.EqualTo(0m));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Warning, "PT-WALL: rate is 0"),
        }));
    }

    [Test]
    public void Reports_only_the_error_when_an_item_with_a_zero_rate_is_left_out()
    {
        var result = Parse(Header + "PT-WALL,Wall paint (2 coats),m2,0,lots\n");

        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "PT-WALL: waste % \"lots\" is not a number"),
        }));
    }

    [TestCase("10", 10)]
    [TestCase("10%", 10)]
    [TestCase("10 %", 10)]
    [TestCase("12.5%", 12.5)]
    [TestCase("0", 0)]
    [TestCase("100", 100)]
    public void Reads_a_waste_percent_with_or_without_a_percent_sign(string wasteText, decimal expected)
    {
        var result = Parse(Header + $"PB-13WR,13mm wet-area plasterboard,m2,24.80,{wasteText}\n");

        result.PriceBook.TryFind("PB-13WR", out var item);
        Assert.That(item.WastePercent, Is.EqualTo(expected));
        Assert.That(result.Problems, Is.Empty);
    }

    [TestCase("-5")]
    [TestCase("-5%")]
    [TestCase("100.5")]
    [TestCase("150%")]
    public void Leaves_out_and_reports_a_waste_percent_outside_0_to_100(string wasteText)
    {
        var result = Parse(Header + $"PB-13WR,13mm wet-area plasterboard,m2,24.80,{wasteText}\n");

        Assert.That(result.PriceBook.TryFind("PB-13WR", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, $"PB-13WR: waste % \"{wasteText}\" is not between 0 and 100"),
        }));
    }

    [TestCase("%")]
    [TestCase("10%%")]
    [TestCase("ten")]
    [TestCase("lots")]
    public void Leaves_out_and_reports_a_waste_percent_that_is_not_a_number(string wasteText)
    {
        var result = Parse(Header + $"PB-13WR,13mm wet-area plasterboard,m2,24.80,{wasteText}\n");

        Assert.That(result.PriceBook.TryFind("PB-13WR", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, $"PB-13WR: waste % \"{wasteText}\" is not a number"),
        }));
    }

    [Test]
    public void Leaves_out_and_reports_an_item_with_no_code()
    {
        var result = Parse(Header + ",10mm plasterboard sheet,m2,18.50,10\n");

        Assert.That(result.PriceBook.TryFind("", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "item code is empty"),
        }));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Leaves_out_and_reports_an_item_with_no_unit(string unit)
    {
        var result = Parse(Header + $"PB-10,10mm plasterboard sheet,{unit},18.50,10\n");

        Assert.That(result.PriceBook.TryFind("PB-10", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "PB-10: unit is empty"),
        }));
    }

    [Test]
    public void Keeps_but_warns_about_an_item_with_no_description()
    {
        var result = Parse(Header + "PB-10,,m2,18.50,10\n");

        Assert.That(result.PriceBook.TryFind("PB-10", out var item), Is.True);
        Assert.That(item.Description, Is.EqualTo(""));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Warning, "PB-10: description is empty"),
        }));
    }

    [Test]
    public void Reports_only_the_error_when_an_item_with_no_description_is_left_out()
    {
        var result = Parse(Header + "AR-66,,m,TBC,5\n");

        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "AR-66: rate \"TBC\" is not a number"),
        }));
    }
}
