using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Contracts;
using Storefront.Application.Emails;
using Storefront.Application.Exceptions;
using Storefront.Domain.Emails;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Emails;

/// <summary>The queue of outgoing emails (task 27b) against a real PostgreSQL, with a clock the tests move.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EmailOutboxDatabaseTests(PostgresFixture postgres)
{
    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public int FailuresLeft { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (FailuresLeft > 0)
            {
                FailuresLeft--;
                throw new EmailDeliveryException("The mail server is down.");
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed record Setup(
        TestDatabase Database,
        WebApplicationFactory<Program> Factory,
        FakeTimeProvider Time,
        RecordingSender Sender) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private async Task<Setup> CreateAsync()
    {
        var database = await postgres.CreateDatabaseAsync();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sender = new RecordingSender();
        var factory = new ApiFactory(database.ConnectionString).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(time);
                services.AddSingleton<IEmailSender>(sender);
            }));
        return new Setup(database, factory, time, sender);
    }

    private static async Task EnqueueAsync(Setup setup, string to = "buyer@example.com")
    {
        await using var scope = setup.Factory.Services.CreateAsyncScope();
        var repositories = scope.ServiceProvider.GetRequiredService<IRepositoryManager>();
        repositories.OutgoingEmails.Enqueue(new EmailMessage(to, "Subject", "Text"));
        await repositories.SaveAsync(default);
    }

    private static async Task<EmailOutboxReport> RunAsync(Setup setup)
    {
        await using var scope = setup.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEmailOutboxService>().SendDueAsync(default);
    }

    private static async Task<OutgoingEmail> OnlyEmailAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return await context.OutgoingEmails.AsNoTracking().SingleAsync();
    }

    [Fact]
    public async Task AQueuedEmail_IsSentOnce_AndMarkedSent()
    {
        await using var setup = await CreateAsync();
        await EnqueueAsync(setup);

        var first = await RunAsync(setup);
        var second = await RunAsync(setup);

        Assert.Equal(1, first.Sent);
        Assert.Equal(0, second.Sent);
        var message = Assert.Single(setup.Sender.Sent);
        Assert.Equal("buyer@example.com", message.To);
        var email = await OnlyEmailAsync(setup);
        Assert.Equal(OutgoingEmailStatus.Sent, email.Status);
        Assert.Equal(1, email.Attempts);
        Assert.NotNull(email.SentAt);
    }

    [Fact]
    public async Task AMailServerThatIsDown_DoesNotLoseTheEmail_ItIsSentAfterThePause()
    {
        await using var setup = await CreateAsync();
        setup.Sender.FailuresLeft = 1;
        await EnqueueAsync(setup);

        var failed = await RunAsync(setup);
        var tooEarly = await RunAsync(setup);
        setup.Time.Advance(TimeSpan.FromSeconds(61));
        var later = await RunAsync(setup);

        Assert.Equal(1, failed.Retried);
        Assert.Equal(0, tooEarly.Sent + tooEarly.Retried);
        Assert.Equal(1, later.Sent);
        var email = await OnlyEmailAsync(setup);
        Assert.Equal(OutgoingEmailStatus.Sent, email.Status);
        Assert.Equal(2, email.Attempts);
    }

    [Fact]
    public async Task ThePauseGrowsWithEveryFailedAttempt()
    {
        await using var setup = await CreateAsync();
        setup.Sender.FailuresLeft = 2;
        await EnqueueAsync(setup);

        await RunAsync(setup);
        setup.Time.Advance(TimeSpan.FromSeconds(61));
        await RunAsync(setup);

        // The second pause is 120 seconds, so 61 seconds later it is still too early.
        setup.Time.Advance(TimeSpan.FromSeconds(61));
        var tooEarly = await RunAsync(setup);
        setup.Time.Advance(TimeSpan.FromSeconds(60));
        var later = await RunAsync(setup);

        Assert.Equal(0, tooEarly.Sent);
        Assert.Equal(1, later.Sent);
    }

    [Fact]
    public async Task AfterTheLastAttempt_TheEmailIsKeptAsFailed_AndNothingSendsItAgain()
    {
        await using var setup = await CreateAsync();
        setup.Sender.FailuresLeft = 100;
        await EnqueueAsync(setup);

        var reports = new List<EmailOutboxReport>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            reports.Add(await RunAsync(setup));
            setup.Time.Advance(TimeSpan.FromHours(1));
        }

        var afterwards = await RunAsync(setup);

        Assert.Equal(4, reports.Sum(r => r.Retried));
        Assert.Equal(1, reports.Sum(r => r.Failed));
        Assert.Equal(0, afterwards.Retried + afterwards.Failed);
        var email = await OnlyEmailAsync(setup);
        Assert.Equal(OutgoingEmailStatus.Failed, email.Status);
        Assert.Equal(5, email.Attempts);
        Assert.Empty(setup.Sender.Sent);
    }

    [Fact]
    public async Task TwoSendersAtTheSameMoment_SendTheEmailOnce()
    {
        await using var setup = await CreateAsync();
        await EnqueueAsync(setup);

        await Task.WhenAll(RunAsync(setup), RunAsync(setup));

        Assert.Single(setup.Sender.Sent);
    }

    [Fact]
    public async Task AnEmailThatAWorkerHolds_IsTakenAgainOnlyAfterTheLease()
    {
        await using var setup = await CreateAsync();
        await EnqueueAsync(setup);
        await using (var scope = setup.Factory.Services.CreateAsyncScope())
        {
            var repositories = scope.ServiceProvider.GetRequiredService<IRepositoryManager>();
            var email = (await repositories.OutgoingEmails.ListDueAsync(setup.Time.GetUtcNow(), 10, default)).Single();
            Assert.True(await repositories.OutgoingEmails.TryClaimAsync(
                email.Id, setup.Time.GetUtcNow(), setup.Time.GetUtcNow().AddSeconds(120), default));
        }

        var whileHeld = await RunAsync(setup);
        setup.Time.Advance(TimeSpan.FromSeconds(121));
        var afterTheLease = await RunAsync(setup);

        Assert.Equal(0, whileHeld.Sent);
        Assert.Equal(1, afterTheLease.Sent);
    }

    [Fact]
    public async Task ASentEmail_IsDeletedAfterTheRetention_AndAPendingOneIsNot()
    {
        await using var setup = await CreateAsync();
        await EnqueueAsync(setup);
        await RunAsync(setup);
        setup.Sender.FailuresLeft = 1;
        await EnqueueAsync(setup, "other@example.com");
        await RunAsync(setup);

        setup.Time.Advance(TimeSpan.FromDays(31));
        setup.Sender.FailuresLeft = 100;
        var report = await RunAsync(setup);

        Assert.Equal(1, report.Deleted);
        await using var context = setup.Database.CreateContext();
        var left = await context.OutgoingEmails.AsNoTracking().SingleAsync();
        Assert.Equal("other@example.com", left.ToAddress);
    }

    [Fact]
    public async Task TheDatabase_RefusesAnUnknownStatusAndANegativeAttemptCount()
    {
        await using var setup = await CreateAsync();
        await EnqueueAsync(setup);

        await using var context = setup.Database.CreateContext();
        var status = await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync("UPDATE outgoing_emails SET status = 'Lost'"));
        var attempts = await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync("UPDATE outgoing_emails SET attempts = -1"));

        Assert.Contains("ck_outgoing_emails_status", status.ToString());
        Assert.Contains("ck_outgoing_emails_attempts", attempts.ToString());
    }
}
