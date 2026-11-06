namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class DatabaseSchemaDocumentTests(PostgresFixture postgres)
{
    [Fact]
    public async Task SchemaDocument_MatchesTheMigrations()
    {
        var database = await postgres.CreateDatabaseAsync();
        var path = Path.Combine(FindRepositoryRoot(), "docs", "database-schema.md");
        var generated = await SchemaDocumentRenderer.RenderAsync(database.ConnectionString);

        if (Environment.GetEnvironmentVariable(SchemaDocumentRenderer.UpdateVariable) == "1")
        {
            await File.WriteAllTextAsync(path, generated);
            return;
        }

        var current = File.Exists(path) ? (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n") : string.Empty;
        Assert.True(
            current == generated,
            $"docs/database-schema.md is out of date. Run with {SchemaDocumentRenderer.UpdateVariable}=1 to update it.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Storefront.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Storefront.slnx was not found.");
    }
}
