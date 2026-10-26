using System.Globalization;

namespace BoqGenerator;

// How numbers are written out. Always with "." whatever the computer's language, and never with
// a currency symbol or thousands separators: 1250.00, never $1,250.00 or 1250,00.
internal static class NumberText
{
    // Quantities and money in the BOQ: exactly 2 decimals.
    public static string TwoDecimals(decimal number) => number.ToString("0.00", CultureInfo.InvariantCulture);

    // A rate: at least 2 decimals, and every decimal the price book gave (18.505), so Quantity x Rate = Total checks out.
    public static string Rate(decimal rate) => rate.ToString("0.00##########################", CultureInfo.InvariantCulture);

    // A number for a problem message, written without trailing zeros: -500.0 is -500, 0.003150 is 0.00315.
    public static string Plain(decimal number) =>
        number.ToString("0.############################", CultureInfo.InvariantCulture);
}
