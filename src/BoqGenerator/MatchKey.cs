namespace BoqGenerator;

// The one rule for when two names written in the files are the same: ignoring case and surrounding
// spaces. Item codes and trades both use it, so "pb-13wr " matches "PB-13WR" and "plasterboard"
// matches "Plasterboard".
internal static class MatchKey
{
    public static string For(string name) => name.Trim().ToUpperInvariant();
}
