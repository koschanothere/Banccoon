static partial class Checks
{
    public static int Failures;
    public static void Check(string label, string actual, string expected)
    {
        if (actual != expected) { Failures++; Console.WriteLine($"FAIL {label}\n   expected: {expected}\n   actual:   {actual}"); }
        else Console.WriteLine($"ok   {label}: {actual}");
    }
}

static class RepoRoot
{
    // Walks up from the working directory to the folder holding Banccoon.sln.
    public static string Find()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Banccoon.sln"))) return dir.FullName;
        }

        throw new InvalidOperationException("Run from inside the Banccoon repository.");
    }
}
