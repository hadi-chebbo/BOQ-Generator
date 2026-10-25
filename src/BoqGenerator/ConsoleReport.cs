namespace BoqGenerator;

// What the user sees after a run: the problems found, then the trade subtotals and totals,
// then where the BOQ was written.
public static class ConsoleReport
{
    public static void Write(
        IReadOnlyList<Problem> problems, string priceBookFile, PricedBoq boq, string outputPath, TextWriter writer)
    {
        WriteProblems(problems, priceBookFile, writer);
        writer.WriteLine();

        WriteMoney(boq, writer);
        writer.WriteLine();

        writer.WriteLine($"BOQ written to {outputPath} ({Words.Plural(boq.Lines.Count, "line")}).");
    }

    // The price book's problems come first, because measurement problems point at them
    // ("see pricebook.csv line 6"). Then each other file in turn, and within each file in line order.
    private static void WriteProblems(IReadOnlyList<Problem> problems, string priceBookFile, TextWriter writer)
    {
        if (problems.Count == 0)
        {
            writer.WriteLine("Problems: none");
            return;
        }

        var errors = problems.Count(problem => problem.IsError);
        var warnings = problems.Count - errors;
        writer.WriteLine($"Problems: {Words.Plural(errors, "error")}, {Words.Plural(warnings, "warning")}");

        var ordered = problems
            .OrderBy(problem => problem.File == priceBookFile ? 0 : 1)
            .ThenBy(problem => problem.File, StringComparer.Ordinal)
            .ThenBy(problem => problem.Line)
            .ToList();

        string Location(Problem problem) => $"{problem.File}:{problem.Line}";
        var locationWidth = ordered.Max(problem => Location(problem).Length);
        var severityWidth = nameof(Severity.Warning).Length;

        foreach (var problem in ordered)
        {
            writer.WriteLine(
                $"  {Location(problem).PadRight(locationWidth)}  {problem.Severity.ToString().PadRight(severityWidth)}  {problem.Message}");
        }
    }

    // The amounts line up in one column so they're easy to read and check.
    private static void WriteMoney(PricedBoq boq, TextWriter writer)
    {
        var subtotals = boq.Trades.Select(trade => (Label: "  " + trade.Trade, Amount: trade.Subtotal)).ToList();
        var totals = new List<(string Label, decimal Amount)>
        {
            ("Total excluding GST", boq.TotalExcludingGst),
            ("GST (10%)", boq.Gst),
            ("Total including GST", boq.TotalIncludingGst),
        };

        var all = subtotals.Concat(totals).ToList();
        var labelWidth = all.Max(row => row.Label.Length);
        var amountWidth = all.Max(row => NumberText.TwoDecimals(row.Amount).Length);
        string Row((string Label, decimal Amount) row) =>
            row.Label.PadRight(labelWidth) + "  " + NumberText.TwoDecimals(row.Amount).PadLeft(amountWidth);

        writer.WriteLine("Trade subtotals (excluding GST):");
        if (subtotals.Count == 0)
        {
            writer.WriteLine("  none");
        }

        foreach (var subtotal in subtotals)
        {
            writer.WriteLine(Row(subtotal));
        }

        writer.WriteLine();
        foreach (var total in totals)
        {
            writer.WriteLine(Row(total));
        }
    }
}
