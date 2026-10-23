namespace BoqGenerator;

// An item code that was in the price book but left out, and the lines it was on.
internal sealed record LeftOutCode(string Code, IReadOnlyList<int> Lines);

// The price items, looked up by item code. Codes match ignoring case and surrounding spaces.
// It also remembers codes that were left out, so a measurement using one can say why it has no price.
public sealed class PriceBook
{
    private readonly Dictionary<string, PriceItem> _items = new();
    private readonly Dictionary<string, LeftOutCode> _leftOut = new();

    // The parser gives each code either one price item or one left-out entry, so Add never meets a duplicate.
    internal PriceBook(IEnumerable<PriceItem> items, IEnumerable<LeftOutCode> leftOut)
    {
        foreach (var item in items)
        {
            _items.Add(Key(item.Code), item);
        }

        foreach (var code in leftOut)
        {
            _leftOut.Add(Key(code.Code), code);
        }
    }

    public bool TryFind(string code, out PriceItem item) => _items.TryGetValue(Key(code), out item!);

    // Whether the code was in the price book but left out, and on which lines.
    public bool TryFindLeftOut(string code, out IReadOnlyList<int> lines)
    {
        var found = _leftOut.TryGetValue(Key(code), out var leftOut);
        lines = found ? leftOut!.Lines : Array.Empty<int>();
        return found;
    }

    // The one rule for when two item codes are the same, shared with the parser's duplicate check.
    internal static string Key(string code) => code.Trim().ToUpperInvariant();
}
