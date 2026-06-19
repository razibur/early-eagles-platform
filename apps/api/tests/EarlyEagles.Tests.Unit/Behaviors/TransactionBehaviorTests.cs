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
        var behavior = new TransactionBehavior<SampleRequest, string>(unitOfWork);

        var response = await behavior.Handle(
            new SampleRequest("ping"),
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
        var behavior = new TransactionBehavior<SampleRequest, string>(unitOfWork);
        var boom = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(
                new SampleRequest("ping"),
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
        var behavior = new TransactionBehavior<SampleRequest, string>(unitOfWork);
        using var cts = new CancellationTokenSource();

        await behavior.Handle(
            new SampleRequest("ping"),
            (_, _) => ValueTask.FromResult("pong"),
            cts.Token);

        Assert.Equal(cts.Token, unitOfWork.LastBeginTransactionToken);
        Assert.Equal(cts.Token, unitOfWork.LastTransaction!.LastCommitToken);
    }

    [Fact]
    public async Task Null_Next_Throws_Argument_Null_Exception()
    {
        var behavior = new TransactionBehavior<SampleRequest, string>(new FakeUnitOfWork());

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.Handle(new SampleRequest("ping"), null!, CancellationToken.None));
    }

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
