using System.Text.Json;

namespace Storefront.Application.Emails;

/// <summary>The texts of the emails to a buyer, from the translation file <c>Texts/bg.json</c> (rule 1: no text in the code).</summary>
internal sealed class EmailTexts
{
    private static readonly Lazy<EmailTexts> Bulgarian = new(() => Load("bg"));

    private readonly Dictionary<string, JsonElement> _entries;

    private EmailTexts(Dictionary<string, JsonElement> entries) => _entries = entries;

    public static EmailTexts Default => Bulgarian.Value;

    /// <summary>A plain text by key, for example <c>lineFormat</c>.</summary>
    public string Text(string key) => _entries[key].GetString()!;

    /// <summary>A subject and a body by key, for example <c>orderPlaced</c>.</summary>
    public (string Subject, string Body) Message(string key) =>
        (_entries[key].GetProperty("subject").GetString()!, _entries[key].GetProperty("body").GetString()!);

    /// <summary>Replaces <c>{name}</c> marks with values. A mark with no value stays as it is, so a typo shows in the test.</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        values.Aggregate(template, (text, pair) => text.Replace("{" + pair.Key + "}", pair.Value, StringComparison.Ordinal));

    private static EmailTexts Load(string language)
    {
        var assembly = typeof(EmailTexts).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".{language}.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var document = JsonDocument.Parse(stream);
        return new EmailTexts(document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()));
    }
}
