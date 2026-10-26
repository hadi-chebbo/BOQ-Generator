namespace BoqGenerator;

internal static class Rounding
{
    // Every quantity and amount of money is rounded the same way: to 2 decimals, with halves away from zero
    // (283.305 is 283.31), as someone checking by hand expects. C#'s default would round halves to even.
    public static decimal ToHundredths(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
