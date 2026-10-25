using System.Text;

namespace BoqGenerator.Tests;

public class AppTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "boq-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    private string InFolder(string name) => Path.Combine(_folder, name);

    private string CopySample(string name)
    {
        var path = InFolder(name);
        File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "data", name), path);
        return path;
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter { NewLine = "\n" };
        var error = new StringWriter { NewLine = "\n" };
        var exitCode = App.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    [Test]
    public void Writes_the_boq_and_prints_the_report_for_the_sample_files()
    {
        var measurements = CopySample("measurements.csv");
        var priceBook = CopySample("pricebook.csv");
        var boqFile = InFolder("boq.csv");

        var (exitCode, output, error) = Run(measurements, priceBook, boqFile);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(error, Is.Empty);

        var writer = new StringWriter();
        BoqCsv.Write(SampleFiles.PriceBoq(), writer);
        var expectedBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(writer.ToString()));
        Assert.That(File.ReadAllBytes(boqFile), Is.EqualTo(expectedBytes));

        Assert.That(output, Does.StartWith("Problems: 10 errors, 2 warnings\n"));
        Assert.That(output, Does.Contain($"  {priceBook}:4 "));
        Assert.That(output, Does.Contain("Total including GST  26984.01\n"));
        Assert.That(output, Does.EndWith($"BOQ written to {boqFile} (11 lines).\n"));
    }

    [Test]
    public void Writes_boq_csv_in_the_current_folder_when_no_output_file_is_given()
    {
        var measurements = CopySample("measurements.csv");
        var priceBook = CopySample("pricebook.csv");
        var previousFolder = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _folder;
        try
        {
            var (exitCode, output, _) = Run(measurements, priceBook);

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(InFolder("boq.csv")), Is.True);
            Assert.That(output, Does.EndWith("BOQ written to boq.csv (11 lines).\n"));
        }
        finally
        {
            Environment.CurrentDirectory = previousFolder;
        }
    }

    [TestCase]
    [TestCase("measurements.csv")]
    [TestCase("a.csv", "b.csv", "c.csv", "d.csv")]
    public void Prints_how_to_use_it_when_the_arguments_are_wrong(params string[] args)
    {
        var (exitCode, output, error) = Run(args);

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(output, Is.Empty);
        Assert.That(error, Does.StartWith("Usage: dotnet run --project src/BoqGenerator -- <measurements.csv> <pricebook.csv> [output.csv]"));
    }

    [Test]
    public void Stops_with_a_message_when_an_input_file_does_not_exist()
    {
        var priceBook = CopySample("pricebook.csv");
        var missing = InFolder("nope.csv");

        var (exitCode, output, error) = Run(missing, priceBook, InFolder("boq.csv"));

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(output, Is.Empty);
        Assert.That(error, Is.EqualTo($"{missing} was not found\n"));
        Assert.That(File.Exists(InFolder("boq.csv")), Is.False);
    }

    [Test]
    public void Stops_with_a_message_when_a_file_cannot_be_used_at_all()
    {
        var measurements = CopySample("measurements.csv");
        var priceBook = InFolder("pricebook.csv");
        File.WriteAllText(priceBook, "Code,Description,Unit,WastePercent\nPB-10,Sheet,m2,10\n");

        var (exitCode, output, error) = Run(measurements, priceBook, InFolder("boq.csv"));

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(output, Is.Empty);
        Assert.That(error, Is.EqualTo($"{priceBook} is missing the column(s): Rate\n"));
        Assert.That(File.Exists(InFolder("boq.csv")), Is.False);
    }

    [Test]
    public void Refuses_to_write_the_boq_over_an_input_file()
    {
        var measurements = CopySample("measurements.csv");
        var priceBook = CopySample("pricebook.csv");
        var before = File.ReadAllBytes(measurements);

        var (exitCode, output, error) = Run(measurements, priceBook, Path.Combine(_folder, ".", "measurements.csv"));

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(output, Is.Empty);
        Assert.That(error, Does.Contain("would overwrite an input file"));
        Assert.That(File.ReadAllBytes(measurements), Is.EqualTo(before));
    }

    [Test]
    public void Refuses_the_same_file_as_both_measurements_and_price_book()
    {
        var measurements = CopySample("measurements.csv");

        var (exitCode, output, error) = Run(measurements, measurements, InFolder("boq.csv"));

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(output, Is.Empty);
        Assert.That(error, Does.Contain("are the same file"));
        Assert.That(File.Exists(InFolder("boq.csv")), Is.False);
    }

    [Test]
    public void Stops_with_a_message_when_the_boq_cannot_be_written()
    {
        var measurements = CopySample("measurements.csv");
        var priceBook = CopySample("pricebook.csv");
        var boqFile = Path.Combine(_folder, "no-such-folder", "boq.csv");

        var (exitCode, output, error) = Run(measurements, priceBook, boqFile);

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(output, Is.Empty);
        Assert.That(error, Is.EqualTo($"Could not write {boqFile}: its folder does not exist\n"));
    }

    [Test]
    public void Stops_with_a_message_when_an_input_file_cannot_be_read()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("File permissions are set differently on Windows.");
        }

        var measurements = CopySample("measurements.csv");
        var priceBook = CopySample("pricebook.csv");
        File.SetUnixFileMode(priceBook, UnixFileMode.None);
        try
        {
            var (exitCode, output, error) = Run(measurements, priceBook, InFolder("boq.csv"));

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output, Is.Empty);
            Assert.That(error, Is.EqualTo($"Could not read {priceBook}: permission denied\n"));
        }
        finally
        {
            File.SetUnixFileMode(priceBook, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Test]
    public void Stops_with_a_message_when_an_input_is_a_folder()
    {
        var priceBook = CopySample("pricebook.csv");
        var folder = InFolder("measurements");
        Directory.CreateDirectory(folder);

        var (exitCode, _, error) = Run(folder, priceBook, InFolder("boq.csv"));

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(error, Is.EqualTo($"{folder} is a folder, not a file\n"));
    }

    [Test]
    public void Refuses_to_write_the_boq_through_a_link_to_an_input_file()
    {
        var measurements = CopySample("measurements.csv");
        var priceBook = CopySample("pricebook.csv");
        var link = InFolder("boq.csv");
        File.CreateSymbolicLink(link, measurements);
        var before = File.ReadAllBytes(measurements);

        var (exitCode, _, error) = Run(measurements, priceBook, link);

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(error, Does.Contain("would overwrite an input file"));
        Assert.That(File.ReadAllBytes(measurements), Is.EqualTo(before));
    }
}
