namespace BoqGenerator.Tests;

public class UnitTests
{
    [TestCase("m2", "m2")]
    [TestCase("M2", "m2")]
    [TestCase("m\u00B2", "m2")]
    [TestCase("M\u00B2", "m2")]
    [TestCase("sqm", "m2")]
    [TestCase("SQM", "m2")]
    [TestCase("sq m", "m2")]
    [TestCase("m", "m")]
    [TestCase("M", "m")]
    [TestCase("lm", "m")]
    [TestCase("LM", "m")]
    [TestCase("mm", "mm")]
    [TestCase("MM", "mm")]
    [TestCase("cm", "cm")]
    [TestCase("CM", "cm")]
    [TestCase("ea", "EA")]
    [TestCase("Ea", "EA")]
    [TestCase("EA", "EA")]
    [TestCase("each", "EA")]
    [TestCase("EACH", "EA")]
    [TestCase("nr", "EA")]
    [TestCase("NR", "EA")]
    [TestCase("  m2  ", "m2")]
    [TestCase("sq  m", "m2")]
    [TestCase("m 2", "m2")]
    [TestCase("e a c h", "EA")]
    public void Reads_every_spelling_of_a_known_unit_as_its_one_name(string text, string expectedName)
    {
        Assert.That(Unit.Read(text).Name, Is.EqualTo(expectedName));
    }

    [TestCase("M2", "m\u00B2")]
    [TestCase("sqm", "m2")]
    [TestCase("each", "EA")]
    [TestCase("ea", "nr")]
    [TestCase("lm", "m")]
    public void Treats_two_spellings_of_a_known_unit_as_the_same_unit(string a, string b)
    {
        Assert.That(Unit.Read(a), Is.EqualTo(Unit.Read(b)));
    }

    [TestCase("m", "m2")]
    [TestCase("mm", "m")]
    [TestCase("m", "cm")]
    [TestCase("EA", "m")]
    public void Treats_different_known_units_as_different(string a, string b)
    {
        Assert.That(Unit.Read(a), Is.Not.EqualTo(Unit.Read(b)));
    }

    [TestCase("kg", "kg")]
    [TestCase(" Bag ", "Bag")]
    public void Keeps_any_other_unit_as_written(string text, string expectedName)
    {
        Assert.That(Unit.Read(text).Name, Is.EqualTo(expectedName));
    }

    [TestCase("kg", "KG")]
    [TestCase("bag", " Bag ")]
    public void Matches_any_other_unit_to_itself_ignoring_case_and_spaces(string a, string b)
    {
        Assert.That(Unit.Read(a), Is.EqualTo(Unit.Read(b)));
    }

    [TestCase("kg", "t")]
    [TestCase("kg", "m2")]
    public void Does_not_match_any_other_unit_to_a_different_unit(string a, string b)
    {
        Assert.That(Unit.Read(a), Is.Not.EqualTo(Unit.Read(b)));
    }

    [Test]
    public void Compares_units_with_the_equals_operator()
    {
        Assert.That(Unit.Read("M2") == Unit.Read("sqm"), Is.True);
        Assert.That(Unit.Read("kg") != Unit.Read("KG"), Is.False);
        Assert.That(Unit.Read("m") == Unit.Read("m2"), Is.False);
    }

    [TestCase(22.8, "M2", "m2")]
    [TestCase(3.42, "m\u00B2", "m2")]
    [TestCase(5, "each", "EA")]
    [TestCase(10, "KG", "kg")]
    public void Keeps_the_quantity_when_both_units_are_the_same(decimal quantity, string from, string to)
    {
        Assert.That(Unit.TryConvert(quantity, Unit.Read(from), Unit.Read(to), out var converted), Is.True);
        Assert.That(converted, Is.EqualTo(quantity));
    }

    [TestCase(41750, "mm", "m", 41.75)]
    [TestCase(4175, "cm", "m", 41.75)]
    [TestCase(2.5, "m", "mm", 2500)]
    [TestCase(2.5, "lm", "cm", 250)]
    [TestCase(12, "cm", "mm", 120)]
    [TestCase(1, "mm", "cm", 0.1)]
    [TestCase(-500, "mm", "m", -0.5)]
    public void Converts_exactly_between_mm_cm_and_m(decimal quantity, string from, string to, decimal expected)
    {
        Assert.That(Unit.TryConvert(quantity, Unit.Read(from), Unit.Read(to), out var converted), Is.True);
        Assert.That(converted, Is.EqualTo(expected));
    }

    [TestCase("m", "m2")]
    [TestCase("mm", "m2")]
    [TestCase("m2", "m")]
    [TestCase("EA", "m")]
    [TestCase("m2", "EA")]
    [TestCase("kg", "t")]
    [TestCase("kg", "m")]
    [TestCase("m", "kg")]
    public void Refuses_to_convert_between_units_with_no_known_conversion(string from, string to)
    {
        Assert.That(Unit.TryConvert(18.7m, Unit.Read(from), Unit.Read(to), out _), Is.False);
    }
}
