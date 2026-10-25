namespace BoqGenerator;

internal static class Words
{
    // "1 error", "3 errors".
    public static string Plural(int count, string thing) => $"{count} {thing}{(count == 1 ? "" : "s")}";
}
