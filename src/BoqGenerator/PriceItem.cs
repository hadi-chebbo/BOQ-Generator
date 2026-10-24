namespace BoqGenerator;

// One item code's entry in the price book. Line is where it was in the file, for problem messages.
public sealed record PriceItem(string Code, string Description, Unit Unit, decimal Rate, decimal WastePercent, int Line);
