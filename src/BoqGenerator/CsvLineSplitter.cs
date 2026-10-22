using System.Text;

namespace BoqGenerator;

public sealed record SplitResult(IReadOnlyList<string> Fields, bool HasUnclosedQuote);

public static class CsvLineSplitter
{
    public static SplitResult Split(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"' && inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (inQuotes)
        {
            return new SplitResult(Array.Empty<string>(), HasUnclosedQuote: true);
        }

        fields.Add(current.ToString().Trim());
        return new SplitResult(fields, HasUnclosedQuote: false);
    }
}
