namespace BoqGenerator;

// One item code in one trade, combining every measurement of it across all plans.
// Quantity is in the price item's unit, before waste. MeasurementLines are where it came from.
// Because of that list, two BoqLines are never == even with the same contents: compare their fields.
public sealed record BoqLine(string Trade, PriceItem PriceItem, decimal Quantity, IReadOnlyList<int> MeasurementLines)
{
    // Code, description and unit come from the price item, so the BOQ shows the price book's spelling.
    public string Code => PriceItem.Code;

    public string Description => PriceItem.Description;

    public Unit Unit => PriceItem.Unit;

    // A problem about the whole line. It has no single row in the file, so it goes on its first measurement's line.
    public Problem Problem(string measurementsFile, Severity severity, string message) =>
        new(measurementsFile, MeasurementLines[0], severity, $"{Code} in {Trade}: {message}");
}
