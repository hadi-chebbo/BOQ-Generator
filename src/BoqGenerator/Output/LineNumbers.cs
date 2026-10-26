namespace BoqGenerator;

internal static class LineNumbers
{
    // How problem messages name other lines: "line 4" or "lines 4, 15".
    public static string Describe(IEnumerable<int> lines)
    {
        var list = lines.ToList();
        return (list.Count == 1 ? "line " : "lines ") + string.Join(", ", list);
    }
}
