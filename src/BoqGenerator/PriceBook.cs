namespace BoqGenerator;

// The price items, looked up by item code. Codes match ignoring case and surrounding spaces.
public sealed class PriceBook
{
    private readonly Dictionary<string, PriceItem> _items = new();

    // The parser gives at most one price item per code, so Add never meets a duplicate.
    public PriceBook(IEnumerable<PriceItem> items)
    {
        foreach (var item in items)
        {
            _items.Add(Key(item.Code), item);
        }
    }

    public bool TryFind(string code, out PriceItem item) => _items.TryGetValue(Key(code), out item!);

    // The one rule for when two item codes are the same, shared with the parser's duplicate check.
    internal static string Key(string code) => code.Trim().ToUpperInvariant();
}
