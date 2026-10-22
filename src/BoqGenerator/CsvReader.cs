namespace BoqGenerator;

public sealed record CsvReadResult(IReadOnlyList<CsvRow> Rows);

public static class CsvReader
{
    public static CsvReadResult Read(TextReader text, string fileName, IReadOnlyList<string> requiredColumns)
    {
        // The header is the first line that isn't blank.
        var lineNumber = 0;
        string? headerLine;
        do
        {
            // Excel's "CSV UTF-8" adds an invisible BOM character before the first column name.
            headerLine = text.ReadLine()?.TrimStart('\uFEFF');
            lineNumber++;
        }
        while (headerLine != null && IsBlank(headerLine));

        if (headerLine == null)
        {
            throw new InputFileException($"{fileName} is empty");
        }

        var columns = ReadHeader(headerLine, fileName, requiredColumns);
        var rows = new List<CsvRow>();

        string? line;
        while ((line = text.ReadLine()) != null)
        {
            lineNumber++;
            var split = CsvLineSplitter.Split(line);
            rows.Add(new CsvRow(lineNumber, columns, split.Fields));
        }

        return new CsvReadResult(rows);
    }

    // Works out which position each column is in. Throws if a required column is missing or appears twice.
    private static Dictionary<string, int> ReadHeader(string headerLine, string fileName, IReadOnlyList<string> requiredColumns)
    {
        var names = CsvLineSplitter.Split(headerLine).Fields;
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < names.Count; i++)
        {
            if (!columns.TryAdd(names[i], i))
            {
                duplicates.Add(names[i]);
            }
        }

        var missing = requiredColumns.Where(name => !columns.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            throw new InputFileException($"{fileName} is missing the column(s): {string.Join(", ", missing)}");
        }

        var duplicated = requiredColumns.Where(name => duplicates.Contains(name)).ToList();
        if (duplicated.Count > 0)
        {
            throw new InputFileException($"{fileName} has the column(s) more than once: {string.Join(", ", duplicated)}");
        }

        return columns;
    }

    // A line with nothing but spaces and commas (",,,," is what Excel leaves behind for a cleared row).
    private static bool IsBlank(string line) => line.All(c => c == ',' || char.IsWhiteSpace(c));
}
