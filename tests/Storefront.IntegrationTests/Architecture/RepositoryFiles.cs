namespace Storefront.IntegrationTests.Architecture;

/// <summary>Finds the repository root and lists source files for the tests that read the repository itself.</summary>
internal static class RepositoryFiles
{
    private static readonly string[] SkippedFolders = ["node_modules", "dist", "bin", "obj", "coverage"];

    public static string Root { get; } = FindRoot();

    public static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath));

    public static string Relative(string fullPath) => Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    /// <summary>All files under a repository folder that match the pattern, without build output and packages.</summary>
    public static IEnumerable<string> Find(string folder, string pattern) =>
        Directory
            .EnumerateFiles(Path.Combine(Root, folder), pattern, SearchOption.AllDirectories)
            .Where(path => !Relative(path).Split('/').Any(part => SkippedFolders.Contains(part)));

    public static int LineCount(string fullPath) => File.ReadAllLines(fullPath).Length;

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Storefront.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Storefront.slnx was not found above the test output folder.");
    }
}
