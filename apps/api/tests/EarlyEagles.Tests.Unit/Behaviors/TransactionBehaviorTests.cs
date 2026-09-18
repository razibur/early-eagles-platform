using EarlyEagles.Application.Abstractions.Persistence;
using EarlyEagles.Application.Behaviors;
using Mediator;

namespace EarlyEagles.Tests.Unit.Behaviors;

public class TransactionBehaviorTests
{
    [Fact]
    public async Task Happy_Path_Begins_Commits_And_Returns_Response()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new TransactionBehavior<SampleCommand, string>(unitOfWork);

        var response = await behavior.Handle(
            new SampleCommand("ping"),
            (_, _) => ValueTask.FromResult("pong"),
            CancellationToken.None);

        Assert.Equal("pong", response);
        Assert.Equal(1, unitOfWork.BeginTransactionCallCount);
        Assert.Equal(1, unitOfWork.LastTransaction!.CommitCallCount);
        Assert.True(unitOfWork.LastTransaction.Disposed);
    }

    [Fact]
    public async Task Handler_Exception_Skips_Commit_And_Disposes_Transaction()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new TransactionBehavior<SampleCommand, string>(unitOfWork);
        var boom = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(
                new SampleCommand("ping"),
                (_, _) => throw boom,
                CancellationToken.None));

        Assert.Same(boom, thrown);
        Assert.Equal(1, unitOfWork.BeginTransactionCallCount);
        Assert.Equal(0, unitOfWork.LastTransaction!.CommitCallCount);
        Assert.True(unitOfWork.LastTransaction.Disposed);
    }

    [Fact]
    public async Task Cancellation_Token_Is_Passed_To_Unit_Of_Work()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new TransactionBehavior<SampleCommand, string>(unitOfWork);
        using var cts = new CancellationTokenSource();

        await behavior.Handle(
            new SampleCommand("ping"),
            (_, _) => ValueTask.FromResult("pong"),
            cts.Token);

        Assert.Equal(cts.Token, unitOfWork.LastBeginTransactionToken);
        Assert.Equal(cts.Token, unitOfWork.LastTransaction!.LastCommitToken);
    }

    [Fact]
    public async Task Null_Next_Throws_Argument_Null_Exception()
    {
        var behavior = new TransactionBehavior<SampleCommand, string>(new FakeUnitOfWork());

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.Handle(new SampleCommand("ping"), null!, CancellationToken.None));
    }

    // P0-024.3 — the behavior wraps WRITES. A read has nothing to commit, so
    // opening a transaction around it is a wasted round trip. Same ICommand gate
    // AuditBehavior uses.
    [Fact]
    public async Task Query_Passes_Through_Without_Opening_A_Transaction()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new TransactionBehavior<SampleQuery, string>(unitOfWork);

        var response = await behavior.Handle(
            new SampleQuery("ping"),
            (_, _) => ValueTask.FromResult("pong"),
            CancellationToken.None);

        Assert.Equal("pong", response);
        Assert.Equal(0, unitOfWork.BeginTransactionCallCount);
        Assert.Null(unitOfWork.LastTransaction);
    }

    [Fact]
    public async Task Plain_Request_Passes_Through_Without_Opening_A_Transaction()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new TransactionBehavior<SampleRequest, string>(unitOfWork);

        var response = await behavior.Handle(
            new SampleRequest("ping"),
            (_, _) => ValueTask.FromResult("pong"),
            CancellationToken.None);

        Assert.Equal("pong", response);
        Assert.Equal(0, unitOfWork.BeginTransactionCallCount);
        Assert.Null(unitOfWork.LastTransaction);
    }

    // Mediator's non-generic ICommand is a separate interface from ICommand<T> —
    // a message implementing only the former must still be wrapped.
    [Fact]
    public async Task Non_Generic_Command_Is_Wrapped_In_A_Transaction()
    {
        var unitOfWork = new FakeUnitOfWork();

        // Fully qualified: the bare name collides with this project's own
        // EarlyEagles.Tests.Unit namespace.
        var behavior = new TransactionBehavior<SampleUnitCommand, Mediator.Unit>(unitOfWork);

        await behavior.Handle(
            new SampleUnitCommand("ping"),
            (_, _) => ValueTask.FromResult(Mediator.Unit.Value),
            CancellationToken.None);

        Assert.Equal(1, unitOfWork.BeginTransactionCallCount);
        Assert.Equal(1, unitOfWork.LastTransaction!.CommitCallCount);
    }

    private sealed record SampleCommand(string Value) : ICommand<string>;

    private sealed record SampleUnitCommand(string Value) : ICommand;

    private sealed record SampleQuery(string Value) : IQuery<string>;

    private sealed record SampleRequest(string Value) : IRequest<string>;

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int BeginTransactionCallCount { get; private set; }

        public CancellationToken LastBeginTransactionToken { get; private set; }

        public FakeTransaction? LastTransaction { get; private set; }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginTransactionCallCount++;
            LastBeginTransactionToken = cancellationToken;
            LastTransaction = new FakeTransaction();
            return Task.FromResult<IUnitOfWorkTransaction>(LastTransaction);
        }
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public int CommitCallCount { get; private set; }

        public CancellationToken LastCommitToken { get; private set; }

        public bool Disposed { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitCallCount++;
            LastCommitToken = cancellationToken;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
