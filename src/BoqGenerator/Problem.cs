namespace BoqGenerator;

public enum Severity
{
    // The data was used, but looks odd and someone should check it.
    Warning,

    // The data was left out of the totals.
    Error,
}

public sealed record Problem(string File, int Line, Severity Severity, string Message);
