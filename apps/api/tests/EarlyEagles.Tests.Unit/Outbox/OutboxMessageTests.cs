using EarlyEagles.Infrastructure.Outbox;

namespace EarlyEagles.Tests.Unit.Outbox;

// P0-050.2 — the row's state machine. A queued message is only ever pending or
// delivered, and the transition between them is the one fact the drain relies
// on: once stamped, a message must never be sent again.
public class OutboxMessageTests
{
    private static readonly Guid TenantId = new("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Marking_Processed_Stamps_The_Row_And_Counts_The_Attempt()
    {
        var message = NewMessage();
        var processedAt = DateTimeOffset.UtcNow;

        message.MarkProcessed(processedAt);

        Assert.Equal(processedAt, message.ProcessedAt);
        Assert.Equal(1, message.AttemptCount);
        Assert.Null(message.LastError);
    }

    [Fact]
    public void Marking_Processed_Clears_A_Previous_Failure()
    {
        // The error described the attempt that failed. Once the message is
        // delivered it no longer describes anything, and leaving it would make
        // a healthy row look broken.
        var message = NewMessage();
        message.RecordFailure("ServiceBusException: the bus was unreachable");

        message.MarkProcessed(DateTimeOffset.UtcNow);

        Assert.Null(message.LastError);
        Assert.Equal(2, message.AttemptCount);
    }

    [Fact]
    public void Marking_Processed_Twice_Is_Refused()
    {
        // Double delivery is a bug in the drain. Throwing makes it a failing
        // test rather than a duplicate every subscriber has to defend against.
        var message = NewMessage();
        message.MarkProcessed(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => message.MarkProcessed(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Marking_Processed_Requires_A_Utc_Timestamp()
    {
        var message = NewMessage();
        var local = new DateTimeOffset(2026, 9, 21, 9, 30, 0, TimeSpan.FromHours(-4));

        Assert.Throws<ArgumentException>(() => message.MarkProcessed(local));
    }

    [Fact]
    public void Recording_A_Failure_Leaves_The_Row_Pending()
    {
        var message = NewMessage();

        message.RecordFailure("ServiceBusException: the bus was unreachable");

        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.AttemptCount);
        Assert.Contains("unreachable", message.LastError!, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_Failures_Keep_Counting()
    {
        // The count is what makes a message that never succeeds visible in the
        // table instead of invisibly looping forever.
        var message = NewMessage();

        message.RecordFailure("first");
        message.RecordFailure("second");

        Assert.Equal(2, message.AttemptCount);
        Assert.Equal("second", message.LastError);
    }

    [Fact]
    public void A_Huge_Error_Is_Truncated_Rather_Than_Rejected()
    {
        // Diagnostic text, not user input: better clipped than lost, and better
        // clipped than bloating a row the poller re-reads every tick.
        var message = NewMessage();

        message.RecordFailure(new string('x', OutboxMessage.LastErrorMaxLength + 500));

        Assert.Equal(OutboxMessage.LastErrorMaxLength, message.LastError!.Length);
    }

    [Fact]
    public void Recording_A_Failure_On_A_Delivered_Message_Is_Refused()
    {
        var message = NewMessage();
        message.MarkProcessed(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => message.RecordFailure("too late"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Blank_Error_Is_Refused(string error)
    {
        var message = NewMessage();

        Assert.Throws<ArgumentException>(() => message.RecordFailure(error));
    }

    private static OutboxMessage NewMessage() =>
        OutboxMessage.Create(
            id: Guid.NewGuid(),
            tenantId: TenantId,
            eventType: "EarlyEagles.Domain.Tenants.SampleEvent",
            payload: "{}",
            occurredAt: DateTimeOffset.UtcNow);
}
