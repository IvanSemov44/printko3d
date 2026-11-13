using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Infrastructure.Email;

namespace Storefront.IntegrationTests.Email;

[Collection(MailpitCollection.Name)]
public sealed class SmtpEmailSenderTests(MailpitFixture mailpit)
{
    private const string From = "shop@example.test";

    private static string NewRecipient() => $"buyer-{Guid.NewGuid():N}@example.test";

    private SmtpEmailSender CreateSender(CapturingLogger logger, EmailOptions? options = null) =>
        new(Options.Create(options ?? Configured()), logger);

    private EmailOptions Configured() => new()
    {
        Host = mailpit.Host,
        Port = mailpit.Port,
        From = From,
        UseTls = false,
    };

    [Fact]
    public async Task Send_DeliversTheMessage_ToTheMailServer()
    {
        var recipient = NewRecipient();
        var sender = CreateSender(new CapturingLogger());

        await sender.SendAsync(new EmailMessage(recipient, "Order 1001", "Thank you for the order.", "<p>Thank you for the order.</p>"), default);

        var caught = Assert.Single(await mailpit.MessagesForAsync(recipient));
        Assert.Equal("Order 1001", caught.Subject);
        Assert.Contains("Thank you for the order.", caught.Text, StringComparison.Ordinal);
        Assert.Contains("<p>Thank you", caught.Html, StringComparison.Ordinal);
        Assert.Equal(From, caught.From.Address);
        Assert.Equal(recipient, Assert.Single(caught.To).Address);
    }

    [Fact]
    public async Task Send_WithoutAHost_SkipsTheMessageWithoutAnError_AndWarns()
    {
        var recipient = NewRecipient();
        var logger = new CapturingLogger();
        var sender = CreateSender(logger, new EmailOptions());
        var message = new EmailMessage(recipient, "Order 1002", "Body text that must not be logged.");

        await sender.SendAsync(message, default);

        Assert.Empty(await mailpit.MessagesForAsync(recipient));
        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("not configured", warning.Text, StringComparison.Ordinal);
        Assert.Contains(message.Id.ToString(), warning.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_WithAHostButNoSender_FailsAndNamesTheSetting()
    {
        var sender = CreateSender(new CapturingLogger(), new EmailOptions { Host = mailpit.Host, Port = mailpit.Port, UseTls = false });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(new EmailMessage(NewRecipient(), "Subject", "Body"), default));

        Assert.Contains("Email:From", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_WhenTheServerIsUnreachable_Throws_WithoutTheRecipientOrTheHostInTheMessageOrTheLog()
    {
        var recipient = NewRecipient();
        var logger = new CapturingLogger();
        var sender = CreateSender(logger, new EmailOptions { Host = "127.0.0.1", Port = 1, From = From, UseTls = false, Password = "secret-password-value" });

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(new EmailMessage(recipient, "Subject", "Body text"), default));

        Assert.DoesNotContain(recipient, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(recipient, logger.AllText(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password-value", logger.AllText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Body text", logger.AllText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_WhenTheRequestIsCancelled_StopsWithoutDelivering()
    {
        var recipient = NewRecipient();
        var sender = CreateSender(new CapturingLogger());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(new EmailMessage(recipient, "Subject", "Body"), cancelled.Token));

        Assert.Empty(await mailpit.MessagesForAsync(recipient));
    }

    /// <summary>Collects what the code writes to the log, so a test can look for what must not be there.</summary>
    private sealed class CapturingLogger : ILogger<SmtpEmailSender>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];

        public string AllText() => string.Join('\n', Entries.Select(entry => entry.Text));

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            Entries.Add((logLevel, exception is null ? text : $"{text} {exception}"));
        }
    }
}
