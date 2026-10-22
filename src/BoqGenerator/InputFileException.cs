namespace BoqGenerator;

// An input file that can't be used at all (missing columns, empty file...). The run stops.
public sealed class InputFileException : Exception
{
    public InputFileException(string message) : base(message)
    {
    }
}
