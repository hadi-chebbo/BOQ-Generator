using System.Globalization;

namespace BoqGenerator;

internal static class NumberText
{
    // A number for a problem message, written without trailing zeros: -500.0 is -500, 0.003150 is 0.00315.
    public static string Plain(decimal number) =>
        number.ToString("0.############################", CultureInfo.InvariantCulture);
}
