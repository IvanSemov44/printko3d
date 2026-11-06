using System.Text;
using Npgsql;

namespace Storefront.IntegrationTests.Persistence;

internal static class SchemaDocumentRenderer
{
    public const string UpdateVariable = "UPDATE_SCHEMA_DOC";

    private const string HistoryTable = "__EFMigrationsHistory";

    public static async Task<string> RenderAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var migration = await ReadAsync(
            connection,
            $"SELECT \"MigrationId\" FROM \"{HistoryTable}\" ORDER BY \"MigrationId\" DESC LIMIT 1",
            reader => reader.GetString(0));
        var columns = await ReadAsync(
            connection,
            """
            SELECT c.relname, a.attname, format_type(a.atttypid, a.atttypmod), a.attnotnull,
                   pg_get_expr(d.adbin, d.adrelid)
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
              AND c.relname <> '__EFMigrationsHistory'
            ORDER BY c.relname, a.attnum
            """,
            reader => new Column(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        var constraints = await ReadAsync(
            connection,
            """
            SELECT cl.relname, con.conname, con.contype::text, pg_get_constraintdef(con.oid)
            FROM pg_constraint con
            JOIN pg_class cl ON cl.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = cl.relnamespace
            WHERE n.nspname = 'public' AND cl.relname <> '__EFMigrationsHistory'
            ORDER BY cl.relname, CASE con.contype WHEN 'p' THEN 1 WHEN 'f' THEN 2 WHEN 'u' THEN 3 ELSE 4 END,
                     con.conname
            """,
            reader => new Constraint(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        var indexes = await ReadAsync(
            connection,
            """
            SELECT tablename, indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
              AND indexname NOT IN (SELECT conname FROM pg_constraint WHERE contype = 'p')
            ORDER BY tablename, indexname
            """,
            reader => new Index(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        var relations = await ReadAsync(
            connection,
            """
            SELECT ref.relname, cl.relname, a.attname, NOT a.attnotnull
            FROM pg_constraint con
            JOIN pg_class cl ON cl.oid = con.conrelid
            JOIN pg_class ref ON ref.oid = con.confrelid
            JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = con.conkey[1]
            WHERE con.contype = 'f' AND cl.relname <> '__EFMigrationsHistory'
            ORDER BY cl.relname, a.attname
            """,
            reader => new Relation(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));

        return Render(migration.Single(), columns, constraints, indexes, relations);
    }

    private static string Render(
        string migration,
        List<Column> columns,
        List<Constraint> constraints,
        List<Index> indexes,
        List<Relation> relations)
    {
        var text = new StringBuilder();
        text.Append("# Схема на базата – каталог на Принтко 3Д\n\n");
        text.Append("> Генериран файл. Не се редактира на ръка.\n");
        text.Append("> Обновява се с `UPDATE_SCHEMA_DOC=1 dotnet test tests/Storefront.IntegrationTests ");
        text.Append("--filter DatabaseSchemaDocumentTests` (изисква Docker). ");
        text.Append("Тестът пропада, когато файлът не съвпада с миграциите.\n");
        text.Append("> Понятията и правилата са в [data-model.md](data-model.md), решението е ");
        text.Append("[0007](decisions/0007-generated-database-schema-document.md).\n\n");
        text.Append($"Последна миграция: `{migration}`\n\n");

        text.Append("## Връзки\n\n```mermaid\nerDiagram\n");
        foreach (var relation in relations)
        {
            var parentSide = relation.Optional ? "|o" : "||";
            text.Append($"    {relation.Parent} {parentSide}--o{{ {relation.Child} : \"{relation.Column}\"\n");
        }

        text.Append("```\n\n## Таблици\n");
        foreach (var table in columns.Select(c => c.Table).Distinct())
        {
            text.Append($"\n### {table}\n\n");
            text.Append("| Колона | Тип | NULL | По подразбиране |\n| --- | --- | --- | --- |\n");
            foreach (var column in columns.Where(c => c.Table == table))
            {
                text.Append($"| {column.Name} | {column.Type} | {(column.NotNull ? "не" : "да")} | ");
                text.Append($"{(column.Default is null ? string.Empty : $"`{Escape(column.Default)}`")} |\n");
            }

            AppendList(text, "Ограничения", constraints.Where(c => c.Table == table)
                .Select(c => $"| {c.Name} | {KindName(c.Kind)} | `{Escape(c.Definition)}` |"),
                "| Име | Вид | Определение |");
            AppendList(text, "Индекси", indexes.Where(i => i.Table == table)
                .Select(i => $"| {i.Name} | `{Escape(i.Definition)}` |"),
                "| Име | Определение |");
        }

        return text.ToString();
    }

    private static void AppendList(StringBuilder text, string title, IEnumerable<string> rows, string header)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return;
        }

        var separator = string.Join('|', header.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(_ => " --- "));
        text.Append($"\n**{title}**\n\n{header}\n|{separator}|\n");
        foreach (var row in list)
        {
            text.Append(row).Append('\n');
        }
    }

    private static string KindName(string kind) => kind switch
    {
        "p" => "първичен ключ",
        "f" => "външен ключ",
        "u" => "уникален",
        "c" => "проверка",
        _ => kind,
    };

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static async Task<List<T>> ReadAsync<T>(
        NpgsqlConnection connection,
        string sql,
        Func<NpgsqlDataReader, T> map)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<T>();
        while (await reader.ReadAsync())
        {
            result.Add(map(reader));
        }

        return result;
    }

    private sealed record Column(string Table, string Name, string Type, bool NotNull, string? Default);

    private sealed record Constraint(string Table, string Name, string Kind, string Definition);

    private sealed record Index(string Table, string Name, string Definition);

    private sealed record Relation(string Parent, string Child, string Column, bool Optional);
}
