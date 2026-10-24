namespace BoqGenerator;

// What a quantity or rate is counted in. A known unit has one name however it's spelled
// (M2, sqm and m with a superscript 2 are all m2). Any other unit, such as kg, is kept as written.
public sealed record Unit
{
    private static readonly Dictionary<string, string> KnownSpellings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["m2"] = "m2", ["m\u00B2"] = "m2", ["sqm"] = "m2",
        ["m"] = "m", ["lm"] = "m",
        ["mm"] = "mm",
        ["cm"] = "cm",
        ["ea"] = "EA", ["each"] = "EA", ["nr"] = "EA",
    };

    private Unit(string name)
    {
        Name = name;
    }

    // The name shown on the BOQ: m2, m, mm, cm or EA for a known unit, otherwise the unit as written.
    public string Name { get; }

    public static Unit Read(string text)
    {
        // Spaces inside a known unit don't matter either: "sq m" and "m 2" are both m2.
        var trimmed = text.Trim();
        var withoutSpaces = string.Concat(trimmed.Where(c => !char.IsWhiteSpace(c)));
        return new Unit(KnownSpellings.TryGetValue(withoutSpaces, out var name) ? name : trimmed);
    }

    // Units are the same if their names match ignoring case, so "kg" and "KG" match too.
    public bool Equals(Unit? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    public override string ToString() => Name;
}
