using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Storefront.IntegrationTests.Email;

/// <summary>
/// A throw-away mail catcher in a container (Mailpit, MIT): it takes every message and sends nothing out, so the real SMTP
/// code runs in tests without internet and without any account. Messages are read back through its HTTP API.
/// </summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int ApiPort = 8025;

    private readonly IContainer _container = new ContainerBuilder("axllent/mailpit:v1.33.1")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(ApiPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(ApiPort)
            .ForPath("/api/v1/info")))
        .Build();

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(SmtpPort);

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>The messages the catcher holds for one recipient. Every test uses its own address, so tests do not disturb each other.</summary>
    public async Task<IReadOnlyList<CaughtMessage>> MessagesForAsync(string recipient)
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(ApiPort)}") };
        var found = await client.GetFromJsonAsync<SearchResult>(
            $"/api/v1/search?query={Uri.EscapeDataString($"to:{recipient}")}");
        var messages = new List<CaughtMessage>();
        foreach (var summary in found!.Messages)
        {
            messages.Add((await client.GetFromJsonAsync<CaughtMessage>($"/api/v1/message/{summary.Id}"))!);
        }

        return messages;
    }

    private sealed record SearchResult([property: JsonPropertyName("messages")] List<Summary> Messages);

    private sealed record Summary([property: JsonPropertyName("ID")] string Id);
}

public sealed record CaughtMessage(
    [property: JsonPropertyName("Subject")] string Subject,
    [property: JsonPropertyName("Text")] string Text,
    [property: JsonPropertyName("HTML")] string Html,
    [property: JsonPropertyName("From")] CaughtAddress From,
    [property: JsonPropertyName("To")] List<CaughtAddress> To);

public sealed record CaughtAddress([property: JsonPropertyName("Address")] string Address);

[CollectionDefinition(Name)]
public sealed class MailpitCollection : ICollectionFixture<MailpitFixture>
{
    public const string Name = "mailpit";
}
