using System.Globalization;
using System.Text.RegularExpressions;

namespace BoqGenerator;

// Reads a number the way people write it in the input files, so the columns that use it agree on what
// a number is. Thousands commas must be in groups of three, so "18,50" is rejected rather than read as
// 1850. ".5" and "+5" are rejected too.
internal static class NumberReader
{
    private const string Digits = @"(\d{1,3}(,\d{3})+|\d+)(\.\d+)?";
    private static readonly Regex Plain = new("^-?" + Digits + "$");
    private static readonly Regex Dollars = new(@"^-?(\$ *)?" + Digits + "$");

    // A plain number: 142.6, 1,250.5, -6.4.
    public static bool TryRead(string text, out decimal number) =>
        TryParse(Plain, text, text, out number);

    // An amount of money, which may have a "$" before the digits: $24.80, $ 24.80, -$6.20.
    public static bool TryReadDollars(string text, out decimal number) =>
        TryParse(Dollars, text, text.Replace("$", "").Replace(" ", ""), out number);

    private static bool TryParse(Regex format, string text, string digits, out decimal number)
    {
        number = 0m;
        return format.IsMatch(text)
            && decimal.TryParse(digits.Replace(",", ""), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out number);
    }
}
