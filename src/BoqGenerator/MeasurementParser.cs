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

        foreach (var row in csv.Rows)
        {
            var code = row["Code"];
            Problem RowProblem(Severity severity, string message) =>
                new(fileName, row.LineNumber, severity, $"{code}: {message}");

            var quantityText = row["Quantity"];
            if (ReadQuantity(quantityText, out var quantity) is { } quantityError)
            {
                problems.Add(RowProblem(Severity.Error, quantityError));
                continue;
            }

            // Warnings come after every error check, so a row that is left out only gets its error.
            if (quantity == 0)
            {
                problems.Add(RowProblem(Severity.Warning, "quantity is 0"));
            }

            if (quantity < 0)
            {
                problems.Add(RowProblem(Severity.Warning, $"quantity {quantityText} is a deduction"));
            }

            measurements.Add(new Measurement(row["Plan"], row["Trade"], code, quantity, row["Unit"], row.LineNumber));
        }

        // The reader's problems come first in the list, so put everything back in file order.
        var problemsInFileOrder = problems.OrderBy(problem => problem.Line).ToList();
        return new MeasurementParseResult(measurements, problemsInFileOrder);
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
