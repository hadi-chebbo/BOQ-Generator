namespace BoqGenerator;

// The price items, looked up by item code. Codes match ignoring case and surrounding spaces.
public sealed class PriceBook
{
    private readonly Dictionary<string, PriceItem> _items = new(StringComparer.OrdinalIgnoreCase);

    public PriceBook(IEnumerable<PriceItem> items)
    {
        foreach (var item in items)
        {
            // For now the first copy of a code wins. Ticket 9 brings the real duplicate rules.
            _items.TryAdd(item.Code.Trim(), item);
        }
    }

    public bool TryFind(string code, out PriceItem item) => _items.TryGetValue(code.Trim(), out item!);
}
