using System.Text.RegularExpressions;

namespace Storefront.IntegrationTests.Architecture;

/// <summary>
/// The size limits of the handbook (B2, B17, F5, F7). A file over its limit is allowed only while docs/tech-debt.md
/// has an open row that names it; paid rows (below "## Платени") do not count. A new big file without a row fails.
/// </summary>
public sealed partial class FileSizeTests
{
    private const int ServiceMaxLines = 300;
    private const int ServiceMaxPublicMethods = 12;
    private const int QueriesMaxLines = 300;
    private const int ComponentMaxLines = 250;
    private const int TestFileMaxLines = 500;

    [Fact]
    public void ServiceFiles_AreWithinTheLimitsOrHaveAnOpenDebtRow()
    {
        var offenders = Files("src/backend", "*.cs")
            .Where(path => RepositoryFiles.Relative(path).Contains("/Services/", StringComparison.Ordinal))
            .Where(path => RepositoryFiles.LineCount(path) > ServiceMaxLines || PublicMethodCount(path) > ServiceMaxPublicMethods)
            .Where(path => !HasOpenDebtRow(path))
            .Select(path => $"{RepositoryFiles.Relative(path)}: {RepositoryFiles.LineCount(path)} lines, {PublicMethodCount(path)} public methods")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void QueryClasses_AreWithinTheLimitOrHaveAnOpenDebtRow() =>
        AssertWithinLimit(Files("src/backend", "*Queries*.cs"), QueriesMaxLines);

    [Fact]
    public void ComponentFiles_AreWithinTheLimitOrHaveAnOpenDebtRow() =>
        AssertWithinLimit(Files("src/web", "*.tsx").Where(path => !IsTestFile(path)), ComponentMaxLines);

    [Fact]
    public void TestFiles_AreWithinTheLimitOrHaveAnOpenDebtRow() =>
        AssertWithinLimit(
            Files("tests", "*.cs").Concat(Files("src/web", "*.test.ts")).Concat(Files("src/web", "*.test.tsx")),
            TestFileMaxLines);

    [Fact]
    public void TheCheck_FindsFilesOfEveryKind()
    {
        // Guards the tests above: a wrong folder or pattern would make them pass with nothing to check.
        Assert.Contains(Files("src/backend", "*.cs"), path => path.Contains("Services", StringComparison.Ordinal));
        Assert.Contains(Files("src/web", "*.tsx"), path => !IsTestFile(path));
        Assert.NotEmpty(Files("src/web", "*.test.tsx"));
        Assert.NotEmpty(Files("tests", "*.cs"));
    }

    private static void AssertWithinLimit(IEnumerable<string> paths, int limit)
    {
        var offenders = paths
            .Where(path => RepositoryFiles.LineCount(path) > limit)
            .Where(path => !HasOpenDebtRow(path))
            .Select(path => $"{RepositoryFiles.Relative(path)}: {RepositoryFiles.LineCount(path)} lines, limit {limit}")
            .ToList();

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> Files(string folder, string pattern) => RepositoryFiles.Find(folder, pattern);

    private static bool IsTestFile(string path) => path.EndsWith(".test.tsx", StringComparison.Ordinal);

    private static int PublicMethodCount(string path) => PublicMethod().Matches(File.ReadAllText(path)).Count;

    // "Foo.cs" is named "Foo" and "foo.test.tsx" is named "foo.test" in the debt register.
    private static bool HasOpenDebtRow(string path) => OpenDebt.Contains(Path.GetFileNameWithoutExtension(path), StringComparison.Ordinal);

    private static string OpenDebt { get; } = ReadOpenDebt();

    private static string ReadOpenDebt()
    {
        var text = RepositoryFiles.Read("docs/tech-debt.md");
        var paid = text.IndexOf("## Платени", StringComparison.Ordinal);

        return paid < 0 ? text : text[..paid];
    }

    // A public method of a class in a file-scoped namespace: four spaces, a return type, a name and "(" or "<".
    // A constructor has no return type and a property has no "(", so neither matches.
    [GeneratedRegex(@"^ {4}public\s[^=;(]*\s\w+\s*[(<]", RegexOptions.Multiline)]
    private static partial Regex PublicMethod();
}
