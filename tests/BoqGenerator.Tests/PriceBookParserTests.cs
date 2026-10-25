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
        Assert.That(item, Is.EqualTo(new PriceItem("PB-10", "10mm plasterboard sheet", Unit.Read("m2"), 18.50m, 10m, Line: 2)));
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
    public void Leaves_out_a_code_whose_copies_have_different_prices()
    {
        var result = Parse(Header + "CN-90,90mm cove cornice,m,6.20,5\nPB-10,10mm plasterboard sheet,m2,18.50,10\ncn-90,90mm cove cornice,m,6.75,5\n");

        Assert.That(result.PriceBook.TryFind("CN-90", out _), Is.False);
        Assert.That(result.PriceBook.TryFind("PB-10", out _), Is.True);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "CN-90: also on line 4 with a different unit, rate or waste %; code left out"),
            new Problem("pricebook.csv", 4, Severity.Error, "cn-90: also on line 2 with a different unit, rate or waste %; code left out"),
        }));
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
    [TestCase("-0")]
    public void Leaves_out_and_reports_a_rate_of_zero(string rateText)
    {
        var result = Parse(Header + $"PT-WALL,Wall paint (2 coats),m2,{rateText},\n");

        Assert.That(result.PriceBook.TryFind("PT-WALL", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "PT-WALL: rate is 0"),
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
    [TestCase("1,000")]
    public void Leaves_out_and_reports_a_waste_percent_outside_0_to_100(string wasteText)
    {
        var result = Parse(Header + $"PB-13WR,13mm wet-area plasterboard,m2,24.80,\"{wasteText}\"\n");

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
    [TestCase(".5")]
    [TestCase("+5")]
    [TestCase("5.")]
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

    [TestCase("CN-90,90mm cove cornice,m,6.20,5")]
    [TestCase("CN-90,Cove cornice 90,M,$6.2,5%")]
    public void Keeps_the_first_of_identical_copies_and_warns_about_the_rest(string secondCopy)
    {
        var result = Parse(Header + "CN-90,90mm cove cornice,m,6.20,5\n" + secondCopy + "\n");

        Assert.That(result.PriceBook.TryFind("CN-90", out var item), Is.True);
        Assert.That(item, Is.EqualTo(new PriceItem("CN-90", "90mm cove cornice", Unit.Read("m"), 6.20m, 5m, Line: 2)));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 3, Severity.Warning, "CN-90: repeats line 2; line 2 is used"),
        }));
    }

    [Test]
    public void Leaves_out_a_code_when_any_copy_cannot_be_used()
    {
        var result = Parse(Header + "CN-90,90mm cove cornice,m,6.20,5\nCN-90,90mm cove cornice,m,TBC,5\n");

        Assert.That(result.PriceBook.TryFind("CN-90", out _), Is.False);
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Error, "CN-90: also on line 3, which can't be used; code left out"),
            new Problem("pricebook.csv", 3, Severity.Error, "CN-90: rate \"TBC\" is not a number"),
        }));
    }

    [Test]
    public void Names_every_other_copy_when_a_code_appears_three_times()
    {
        var result = Parse(Header + "CN-90,Cornice,m,6.20,5\nCN-90,Cornice,m,6.20,5\nCN-90,Cornice,m,6.75,5\n");

        Assert.That(result.PriceBook.TryFind("CN-90", out _), Is.False);
        Assert.That(result.Problems.Select(problem => problem.Message), Is.EqualTo(new[]
        {
            "CN-90: also on lines 3, 4 with a different unit, rate or waste %; code left out",
            "CN-90: also on lines 2, 4 with a different unit, rate or waste %; code left out",
            "CN-90: also on lines 2, 3 with a different unit, rate or waste %; code left out",
        }));
    }

    [Test]
    public void Remembers_a_code_that_was_left_out_and_its_line()
    {
        var result = Parse(Header + "PB-10,10mm plasterboard sheet,m2,18.50,10\nAR-66,66mm pine architrave,m,TBC,5\n");

        Assert.That(result.PriceBook.TryFindLeftOut("AR-66", out var lines), Is.True);
        Assert.That(lines, Is.EqualTo(new[] { 3 }));
    }

    [TestCase("6.75")]
    [TestCase("TBC")]
    public void Remembers_every_line_of_a_duplicate_code_that_was_left_out(string secondRate)
    {
        var result = Parse(Header + $"CN-90,90mm cove cornice,m,6.20,5\nPB-10,10mm plasterboard sheet,m2,18.50,10\nCN-90,90mm cove cornice,m,{secondRate},5\n");

        Assert.That(result.PriceBook.TryFindLeftOut(" cn-90 ", out var lines), Is.True);
        Assert.That(lines, Is.EqualTo(new[] { 2, 4 }));
    }

    [TestCase("PB-10")]
    [TestCase("XX-99")]
    public void Does_not_count_a_used_or_unknown_code_as_left_out(string code)
    {
        var result = Parse(Header + "PB-10,10mm plasterboard sheet,m2,18.50,10\n");

        Assert.That(result.PriceBook.TryFindLeftOut(code, out var lines), Is.False);
        Assert.That(lines, Is.Empty);
    }

    [TestCase("M2", "m2")]
    [TestCase("each", "EA")]
    [TestCase("kg", "kg")]
    public void Recognises_the_unit_of_a_price_item(string unitText, string expectedName)
    {
        var result = Parse(Header + $"PB-10,10mm plasterboard sheet,{unitText},18.50,10\n");

        Assert.That(result.PriceBook.TryFind("PB-10", out var item), Is.True);
        Assert.That(item.Unit.Name, Is.EqualTo(expectedName));
    }

    [TestCase("m\u00B2")]
    [TestCase("sqm")]
    public void Treats_copies_whose_units_are_spelled_differently_as_identical(string secondUnit)
    {
        var result = Parse(Header + $"PB-10,10mm plasterboard sheet,m2,18.50,10\nPB-10,10mm plasterboard sheet,{secondUnit},18.50,10\n");

        Assert.That(result.PriceBook.TryFind("PB-10", out var item), Is.True);
        Assert.That(item.Line, Is.EqualTo(2));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 3, Severity.Warning, "PB-10: repeats line 2; line 2 is used"),
        }));
    }

    [TestCase("")]
    [TestCase("\n,,,,\n   \n")]
    public void Stops_when_the_price_book_has_no_rows_after_its_header(string afterHeader)
    {
        var error = Assert.Throws<InputFileException>(() => Parse(Header + afterHeader));

        Assert.That(error.Message, Is.EqualTo("pricebook.csv has no rows"));
    }

    [Test]
    public void Still_reports_each_row_when_every_row_is_bad()
    {
        var result = Parse(Header + "PB-10,\"10mm plasterboard,m2,18.50,10\n");

        Assert.That(result.Problems, Has.Count.EqualTo(1));
    }

    [TestCase("0.1", 0.1, "10")]
    [TestCase("0.5", 0.5, "50")]
    [TestCase("0.05", 0.05, "5")]
    public void Keeps_but_warns_about_a_waste_percent_that_looks_like_a_fraction(string wasteText, decimal expected, string meant)
    {
        var result = Parse(Header + $"PB-10,10mm plasterboard sheet,m2,18.50,{wasteText}\n");

        Assert.That(result.PriceBook.TryFind("PB-10", out var item), Is.True);
        Assert.That(item.WastePercent, Is.EqualTo(expected));
        Assert.That(result.Problems, Is.EqualTo(new[]
        {
            new Problem("pricebook.csv", 2, Severity.Warning, $"PB-10: waste % \"{wasteText}\" is very small; did you mean {meant}%?"),
        }));
    }

    [TestCase("0")]
    [TestCase("1")]
    [TestCase("")]
    [TestCase("0.5%")]
    [TestCase("0.1 %")]
    public void Does_not_warn_about_no_waste_a_whole_percent_or_a_percent_written_with_its_sign(string wasteText)
    {
        var result = Parse(Header + $"PB-10,10mm plasterboard sheet,m2,18.50,{wasteText}\n");

        Assert.That(result.Problems, Is.Empty);
    }
}
