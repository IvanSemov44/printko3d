namespace Storefront.IntegrationTests.Architecture;

/// <summary>docs/architecture.md must list every folder under features/ and every controller. A mismatch is an error, not debt.</summary>
public sealed class ArchitectureMapTests
{
    private static readonly string[] WebApps = ["storefront", "admin"];

    [Fact]
    public void EveryFeatureFolder_IsListedInTheRowOfItsApp()
    {
        var missing = new List<string>();
        foreach (var app in WebApps)
        {
            var row = TableRow($"| `{app}/`");
            var features = Directory.GetDirectories(Path.Combine(RepositoryFiles.Root, "src/web", app, "src/features")).Select(Path.GetFileName);
            Assert.NotEmpty(features);

            missing.AddRange(features.Where(feature => !row.Contains($"`{feature}`", StringComparison.Ordinal)).Select(feature => $"{app}/src/features/{feature}"));
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryController_IsListedInTheControllersRow()
    {
        var row = TableRow("| `Controllers/`");
        var controllers = RepositoryFiles
            .Find("src/backend/Storefront.Api/Controllers", "*Controller.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();
        Assert.NotEmpty(controllers);

        Assert.DoesNotContain(controllers, controller => !row.Contains($"`{controller}`", StringComparison.Ordinal));
    }

    private static string TableRow(string start)
    {
        var rows = RepositoryFiles
            .Read("docs/architecture.md")
            .Split('\n')
            .Where(line => line.StartsWith(start, StringComparison.Ordinal))
            .ToList();

        Assert.True(rows.Count == 1, $"Expected one row of docs/architecture.md that starts with {start}, found {rows.Count}.");

        return rows[0];
    }
}
