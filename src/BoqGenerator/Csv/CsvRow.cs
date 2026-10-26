namespace BoqGenerator;

public sealed class CsvRow
{
    private readonly IReadOnlyDictionary<string, int> _columns;
    private readonly IReadOnlyList<string> _fields;

    public CsvRow(int lineNumber, IReadOnlyDictionary<string, int> columns, IReadOnlyList<string> fields)
    {
        LineNumber = lineNumber;
        _columns = columns;
        _fields = fields;
    }

    public int LineNumber { get; }

    public string this[string column] => _fields[_columns[column]];
}
