using System.Text;

namespace BoqGenerator;

// The command line program: reads the two input files, writes the BOQ and prints the report.
// Returns the exit code: 0 when the BOQ was written (even if some rows had problems), 1 otherwise.
// With --strict, a run whose data had any Errors also exits with 1, after writing the BOQ as usual.
// Every expected failure is a one-line message on the error output, never a crash.
public static class App
{
    private const string Usage =
        "Usage: dotnet run --project src/BoqGenerator -- [--strict] <measurements.csv> <pricebook.csv> [output.csv]";

    private const string StrictFlag = "--strict";

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        // --strict can go anywhere. Any other word starting with "-" is a mistyped flag, never a file name
        // (a file whose name starts with "-" can be given as ./-file.csv).
        var strict = args.Contains(StrictFlag);
        var files = args.Where(arg => arg != StrictFlag).ToArray();
        if (files.Length is < 2 or > 3 || files.Any(arg => arg.StartsWith('-')))
        {
            error.WriteLine(Usage);
            return 1;
        }

        var measurementsFile = files[0];
        var priceBookFile = files[1];
        var outputFile = files.Length == 3 ? files[2] : "boq.csv";

        if (CheckFiles(measurementsFile, priceBookFile, outputFile) is { } fileProblem)
        {
            error.WriteLine(fileProblem);
            return 1;
        }

        using var measurementsText = OpenInput(measurementsFile, error);
        using var priceBookText = OpenInput(priceBookFile, error);
        if (measurementsText == null || priceBookText == null)
        {
            return 1;
        }

        GeneratedBoq generated;
        try
        {
            generated = BoqGeneration.Generate(measurementsText, measurementsFile, priceBookText, priceBookFile);
        }
        catch (InputFileException unusable)
        {
            // A whole file can't be used (a missing column, an empty file), so there's no BOQ to write.
            error.WriteLine(unusable.Message);
            return 1;
        }

        try
        {
            // UTF-8 with a BOM, so Excel shows any non-English character correctly.
            using var boqText = new StreamWriter(outputFile, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            BoqCsv.Write(generated.Boq, boqText);
        }
        catch (Exception cannotWrite) when (cannotWrite is IOException or UnauthorizedAccessException)
        {
            // A missing folder, no permission, or the file is open in Excel.
            error.WriteLine($"Could not write {outputFile}: {Reason(cannotWrite)}");
            return 1;
        }

        ConsoleReport.Write(generated.Problems, priceBookFile, generated.Boq, outputFile, output);

        var errors = generated.Problems.Count(problem => problem.IsError);
        if (strict && errors > 0)
        {
            error.WriteLine($"{StrictFlag}: {Words.Plural(errors, "error")}, so exit code 1");
            return 1;
        }

        return 0;
    }

    // Returns why the files can't be used, or null if they can.
    private static string? CheckFiles(string measurementsFile, string priceBookFile, string outputFile)
    {
        foreach (var inputFile in new[] { measurementsFile, priceBookFile })
        {
            if (Directory.Exists(inputFile))
            {
                return $"{inputFile} is a folder, not a file";
            }

            if (!File.Exists(inputFile))
            {
                return $"{inputFile} was not found";
            }
        }

        if (SameFile(measurementsFile, priceBookFile))
        {
            return $"{measurementsFile} and {priceBookFile} are the same file; give the measurements and the price book separately";
        }

        if (SameFile(outputFile, measurementsFile) || SameFile(outputFile, priceBookFile))
        {
            return $"The output file {outputFile} would overwrite an input file; choose another name";
        }

        return null;
    }

    // "data/a.csv", "./data/a.csv" and a link to it are all the same file.
    // Windows and macOS ignore case in file names; Linux doesn't.
    private static bool SameFile(string a, string b)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(RealPath(a), RealPath(b), comparison);
    }

    // The full path, following a link to the file it points at.
    private static string RealPath(string path)
    {
        var file = new FileInfo(path);
        var target = file.Exists ? file.ResolveLinkTarget(returnFinalTarget: true) : null;
        return Path.GetFullPath(target?.FullName ?? path);
    }

    // Opens an input file, or reports why it can't be read (no permission, or locked by another program).
    private static StreamReader? OpenInput(string file, TextWriter error)
    {
        try
        {
            return File.OpenText(file);
        }
        catch (Exception cannotRead) when (cannotRead is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Could not read {file}: {Reason(cannotRead)}");
            return null;
        }
    }

    // A short reason in plain words. .NET's own messages repeat the full path, not the name the user typed.
    private static string Reason(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "permission denied",
        DirectoryNotFoundException => "its folder does not exist",
        _ when exception.Message.Contains("being used by another process") => "it is open in another program",
        _ => "the file could not be opened",
    };
}
