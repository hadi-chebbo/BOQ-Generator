using System.Text;

namespace BoqGenerator;

public sealed record SplitResult(IReadOnlyList<string> Fields, bool HasQuoteError);

public static class CsvLineSplitter
{
    private static readonly SplitResult QuoteError = new(Array.Empty<string>(), HasQuoteError: true);

    public static SplitResult Split(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var quoteJustClosed = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"' && inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else if (c == '"' && inQuotes)
            {
                inQuotes = false;
                quoteJustClosed = true;
            }
            else if (c == '"' && string.IsNullOrWhiteSpace(current.ToString()))
            {
                inQuotes = true;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
                quoteJustClosed = false;
            }
            else if (quoteJustClosed && !char.IsWhiteSpace(c))
            {
                return QuoteError;
            }
            else
            {
                current.Append(c);
            }
        }

        if (inQuotes)
        {
            return QuoteError;
        }

        fields.Add(current.ToString().Trim());
        return new SplitResult(fields, HasQuoteError: false);
    }
}
