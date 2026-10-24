namespace BoqGenerator;

public sealed record MeasurementParseResult(IReadOnlyList<Measurement> Measurements, IReadOnlyList<Problem> Problems);

public static class MeasurementParser
{
    private static readonly string[] RequiredColumns = { "Plan", "Trade", "Code", "Quantity", "Unit" };

    public static MeasurementParseResult Parse(TextReader text, string fileName)
    {
        var csv = CsvReader.Read(text, fileName, RequiredColumns);
        var measurements = new List<Measurement>();
        var problems = new List<Problem>(csv.Problems);

        foreach (var checkedRow in csv.Rows.Select(row => CheckRow(row, fileName)))
        {
            problems.AddRange(checkedRow.Problems);
            if (checkedRow.Measurement != null)
            {
                measurements.Add(checkedRow.Measurement);
            }
        }

        // The reader's problems come first in the list, so put everything back in file order.
        var problemsInFileOrder = problems.OrderBy(problem => problem.Line).ToList();
        return new MeasurementParseResult(measurements, problemsInFileOrder);
    }

    // One measurements row after its values are checked: the measurement, or null if the row can't be used,
    // plus the row's own problems.
    private sealed record CheckedRow(Measurement? Measurement, IReadOnlyList<Problem> Problems);

    private static CheckedRow CheckRow(CsvRow row, string fileName)
    {
        CheckedRow Unusable(Problem error) => new(null, new[] { error });

        // There's no code to start the message with, so this one is built on its own.
        var code = row["Code"];
        if (code.Length == 0)
        {
            return Unusable(new Problem(fileName, row.LineNumber, Severity.Error, "item code is empty"));
        }

        Problem RowProblem(Severity severity, string message) =>
            new(fileName, row.LineNumber, severity, $"{code}: {message}");

        var trade = row["Trade"];
        if (trade.Length == 0)
        {
            return Unusable(RowProblem(Severity.Error, "trade is empty"));
        }

        var quantityText = row["Quantity"];
        if (ReadQuantity(quantityText, out var quantity) is { } quantityError)
        {
            return Unusable(RowProblem(Severity.Error, quantityError));
        }

        var unit = row["Unit"];
        if (unit.Length == 0)
        {
            return Unusable(RowProblem(Severity.Error, "unit is empty"));
        }

        // Warnings come after every error check, so a row that is left out only gets its error.
        var warnings = new List<Problem>();
        var plan = row["Plan"];
        if (plan.Length == 0)
        {
            warnings.Add(RowProblem(Severity.Warning, "plan is empty"));
        }

        if (quantity == 0)
        {
            warnings.Add(RowProblem(Severity.Warning, "quantity is 0"));
        }

        if (quantity < 0)
        {
            warnings.Add(RowProblem(Severity.Warning, $"quantity {quantityText} is a deduction"));
        }

        var measurement = new Measurement(plan, trade, code, quantity, Unit.Read(unit), row.LineNumber);
        return new CheckedRow(measurement, warnings);
    }

    // Returns why the quantity can't be used, or null if it can.
    private static string? ReadQuantity(string text, out decimal quantity)
    {
        quantity = 0m;
        if (text.Length == 0)
        {
            return "quantity is empty";
        }

        return NumberReader.TryRead(text, out quantity) ? null : $"quantity \"{text}\" is not a number";
    }
}
