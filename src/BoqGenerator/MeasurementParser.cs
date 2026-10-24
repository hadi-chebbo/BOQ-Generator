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
            var quantityText = row["Quantity"];

            // For now any quantity that isn't a plain number is left out. Ticket 13 adds the full quantity rules.
            if (!NumberReader.TryRead(quantityText, out var quantity))
            {
                problems.Add(new Problem(fileName, row.LineNumber, Severity.Error, $"{code}: quantity \"{quantityText}\" is not a number"));
                continue;
            }

            measurements.Add(new Measurement(row["Plan"], row["Trade"], code, quantity, row["Unit"], row.LineNumber));
        }

        // The reader's problems come first in the list, so put everything back in file order.
        var problemsInFileOrder = problems.OrderBy(problem => problem.Line).ToList();
        return new MeasurementParseResult(measurements, problemsInFileOrder);
    }
}
