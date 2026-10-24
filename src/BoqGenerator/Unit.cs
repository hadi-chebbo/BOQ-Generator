namespace BoqGenerator;

// What a quantity or rate is counted in. A known unit has one name however it's spelled
// (M2, sqm and m with a superscript 2 are all m2). Any other unit, such as kg, is kept as written.
public sealed record Unit
{
    private static readonly Unit SquareMetre = new("m2", millimetres: null);
    private static readonly Unit Metre = new("m", millimetres: 1000);
    private static readonly Unit Centimetre = new("cm", millimetres: 10);
    private static readonly Unit Millimetre = new("mm", millimetres: 1);
    private static readonly Unit Each = new("EA", millimetres: null);

    private static readonly Dictionary<string, Unit> KnownSpellings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["m2"] = SquareMetre, ["m\u00B2"] = SquareMetre, ["sqm"] = SquareMetre,
        ["m"] = Metre, ["lm"] = Metre,
        ["cm"] = Centimetre,
        ["mm"] = Millimetre,
        ["ea"] = Each, ["each"] = Each, ["nr"] = Each,
    };

    // How long one of this unit is, for length units only. Lengths convert to each other through it.
    // Every spelling of a known unit gives the same instance above, so equal names always mean equal sizes.
    private readonly decimal? _millimetres;

    private Unit(string name, decimal? millimetres)
    {
        Name = name;
        _millimetres = millimetres;
    }

    // The name shown on the BOQ: m2, m, mm, cm or EA for a known unit, otherwise the unit as written.
    public string Name { get; }

    // Whether this unit counts whole items (EA) rather than measuring an amount.
    public bool IsCount => this == Each;

    public static Unit Read(string text)
    {
        // Spaces inside a known unit don't matter either: "sq m" and "m 2" are both m2.
        var trimmed = text.Trim();
        var withoutSpaces = string.Concat(trimmed.Where(c => !char.IsWhiteSpace(c)));
        return KnownSpellings.TryGetValue(withoutSpaces, out var known) ? known : new Unit(trimmed, millimetres: null);
    }

    // Converts a quantity to another unit when that's safe: the same unit, or one length to another
    // (mm, cm, m). Anything else, like m to m2 or kg to t, can't be converted.
    public static bool TryConvert(decimal quantity, Unit from, Unit to, out decimal converted)
    {
        if (from == to)
        {
            converted = quantity;
            return true;
        }

        if (from._millimetres is { } fromSize && to._millimetres is { } toSize)
        {
            converted = quantity * fromSize / toSize;
            return true;
        }

        converted = 0m;
        return false;
    }

    // Units are the same if their names match ignoring case, so "kg" and "KG" match too.
    public bool Equals(Unit? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    public override string ToString() => Name;
}
