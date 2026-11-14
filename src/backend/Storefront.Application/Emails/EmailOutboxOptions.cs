namespace Storefront.Application.Emails;

/// <summary>Settings of the queue of outgoing emails (decision 0036). Bound from the <c>EmailOutbox</c> section and checked at startup.</summary>
public sealed class EmailOutboxOptions
{
    public const string SectionName = "EmailOutbox";

    /// <summary>Seconds between two runs of the sender.</summary>
    public int IntervalSeconds { get; init; } = 15;

    /// <summary>Seconds to wait after the start before the first run.</summary>
    public int InitialDelaySeconds { get; init; } = 10;

    /// <summary>Messages taken in one read. A full batch means there may be more, and the run goes on.</summary>
    public int BatchSize { get; init; } = 20;

    /// <summary>How many times a message is tried before it is kept as failed.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Seconds before the second attempt. Every next pause is twice as long.</summary>
    public int RetryBaseSeconds { get; init; } = 60;

    /// <summary>Seconds a worker holds a message while it sends it. After that the message can be taken again, as after a crash.</summary>
    public int LeaseSeconds { get; init; } = 120;

    /// <summary>Days a sent message is kept. It holds the address and the text of the buyer, so it is not kept for long.</summary>
    public int RetentionDays { get; init; } = 30;

    public static bool IsValid(EmailOutboxOptions options) =>
        options.IntervalSeconds is >= 1 and <= 3600
        && options.InitialDelaySeconds is >= 0 and <= 3600
        && options.BatchSize is >= 1 and <= 500
        && options.MaxAttempts is >= 1 and <= 20
        && options.RetryBaseSeconds is >= 1 and <= 86_400
        && options.LeaseSeconds is >= 1 and <= 3600
        && options.RetentionDays is >= 1 and <= 3650;
}
