namespace BoqGenerator;

// One quantity measured from a plan. Line is where it was in the file, for problem messages.
public sealed record Measurement(string Plan, string Trade, string Code, decimal Quantity, Unit Unit, int Line);
