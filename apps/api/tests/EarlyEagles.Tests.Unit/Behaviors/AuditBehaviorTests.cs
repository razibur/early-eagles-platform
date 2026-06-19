using EarlyEagles.Application.Abstractions.Auditing;
using EarlyEagles.Application.Behaviors;
using Mediator;

namespace EarlyEagles.Tests.Unit.Behaviors;

public class AuditBehaviorTests
{
    [Fact]
    public async Task Non_Command_Request_Is_Not_Audited()
    {
        var audit = new CapturingAuditService();
        var behavior = new AuditBehavior<SampleQuery, string>(audit);

        var response = await behavior.Handle(
            new SampleQuery("q"),
            (_, _) => ValueTask.FromResult("answer"),
            CancellationToken.None);

        Assert.Equal("answer", response);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Command_Success_Logs_Success_Entry()
    {
        var audit = new CapturingAuditService();
        var behavior = new AuditBehavior<SampleCommand, string>(audit);

        var response = await behavior.Handle(
            new SampleCommand("do-it"),
            (_, _) => ValueTask.FromResult("done"),
            CancellationToken.None);

        Assert.Equal("done", response);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(nameof(SampleCommand), entry.Action);
        Assert.True(entry.Success);
        Assert.Null(entry.FailureReason);
    }

    [Fact]
    public async Task Command_Exception_Logs_Failure_Entry_And_Rethrows()
    {
        var audit = new CapturingAuditService();
        var behavior = new AuditBehavior<SampleCommand, string>(audit);
        var boom = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(
                new SampleCommand("do-it"),
                (_, _) => throw boom,
                CancellationToken.None));

        Assert.Same(boom, thrown);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(nameof(SampleCommand), entry.Action);
        Assert.False(entry.Success);
        Assert.NotNull(entry.FailureReason);
        Assert.Contains("InvalidOperationException", entry.FailureReason);
        Assert.Contains("boom", entry.FailureReason);
    }

    [Fact]
    public async Task Cancellation_Propagates_Without_Audit_Entry()
    {
        var audit = new CapturingAuditService();
        var behavior = new AuditBehavior<SampleCommand, string>(audit);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await behavior.Handle(
                new SampleCommand("do-it"),
                (_, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult("never");
                },
                cts.Token));

        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Null_Next_Throws_Argument_Null_Exception()
    {
        var behavior = new AuditBehavior<SampleCommand, string>(new CapturingAuditService());

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.Handle(new SampleCommand("x"), null!, CancellationToken.None));
    }

    private sealed record SampleQuery(string Q) : IQuery<string>;

    private sealed record SampleCommand(string Cmd) : ICommand<string>;

    private sealed class CapturingAuditService : IAuditService
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
