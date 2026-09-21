namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// Tuning for the background outbox drain (<see cref="OutboxProcessor"/>),
/// bound from the <c>Outbox</c> configuration section.
/// </summary>
/// <remarks>
/// The defaults suit a single small instance: a ten-second lag between a
/// command committing and its event reaching the bus is invisible to a member
/// waiting on a notification, and fifty rows is well under what one round trip
/// can carry. Both are worth raising only once real traffic shows the queue
/// growing faster than a tick can drain it.
/// </remarks>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public const int MaxBatchSize = 1000;

    /// <summary>
    /// Kill switch. Off means the processor starts, says so, and never touches
    /// the database — the way to stop a poison batch from being retried during
    /// an incident without shipping a deploy. Rows keep accumulating; nothing is
    /// lost, delivery is just paused.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How long to wait between drains.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Most rows one tick will claim.</summary>
    public int BatchSize { get; set; } = 50;
}
