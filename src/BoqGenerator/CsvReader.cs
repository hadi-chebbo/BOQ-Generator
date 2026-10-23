namespace BoqGenerator;

public sealed record CsvReadResult(IReadOnlyList<CsvRow> Rows, IReadOnlyList<Problem> Problems);

public static class CsvReader
{
    public static CsvReadResult Read(TextReader text, string fileName, IReadOnlyList<string> requiredColumns)
    {
        // Excel's "CSV UTF-8" adds an invisible BOM character at the very start of the file.
        var headerLine = text.ReadLine()?.TrimStart('\uFEFF');
        var lineNumber = 1;

        // The header is the first line that isn't blank.
        while (headerLine != null && IsBlank(headerLine))
        {
            headerLine = text.ReadLine();
            lineNumber++;
        }

        if (headerLine == null)
        {
            throw new InputFileException($"{fileName} is empty");
        }

        var headerSplit = CsvLineSplitter.Split(headerLine);
        if (headerSplit.HasQuoteError)
        {
            throw new InputFileException($"{fileName}: the header on line {lineNumber} has a badly quoted column name");
        }

        var columnCount = headerSplit.Fields.Count;
        var columns = ReadColumnPositions(headerSplit.Fields, fileName, requiredColumns);
        var rows = new List<CsvRow>();
        var problems = new List<Problem>();

        string? line;
        while ((line = text.ReadLine()) != null)
        {
            lineNumber++;
            if (IsBlank(line))
            {
                continue;
            }

            var rowSplit = CsvLineSplitter.Split(line);
            if (rowSplit.HasQuoteError)
            {
                problems.Add(new Problem(fileName, lineNumber, Severity.Error, "has a badly quoted value"));
                continue;
            }

            var fields = rowSplit.Fields;
            var valueCount = CountUpToLastValue(fields);
            if (valueCount > columnCount)
            {
                problems.Add(new Problem(fileName, lineNumber, Severity.Error,
                    $"has {valueCount} values but the header has {columnCount} columns"));
                continue;
            }

            rows.Add(new CsvRow(lineNumber, columns, PadToColumnCount(fields, columnCount)));
        }

        return new CsvReadResult(rows, problems);
    }

    // Works out which position each column is in. Throws if a required column is missing or appears twice.
    private static Dictionary<string, int> ReadColumnPositions(
        IReadOnlyList<string> names, string fileName, IReadOnlyList<string> requiredColumns)
    {
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

    // How many fields there are up to the last non-empty one, so trailing empty fields don't count.
    private static int CountUpToLastValue(IReadOnlyList<string> fields)
    {
        var count = fields.Count;
        while (count > 0 && fields[count - 1].Length == 0)
        {
            count--;
        }

        return count;
    }

    // A short row reads as empty values at the end, the same as it looks in Excel.
    private static IReadOnlyList<string> PadToColumnCount(IReadOnlyList<string> fields, int columnCount)
    {
        if (fields.Count >= columnCount)
        {
            return fields;
        }

        return fields.Concat(Enumerable.Repeat("", columnCount - fields.Count)).ToList();
    }

    // A line with nothing but spaces and commas (",,,," is what Excel leaves behind for a cleared row).
    private static bool IsBlank(string line) => line.All(c => c == ',' || char.IsWhiteSpace(c));
}
