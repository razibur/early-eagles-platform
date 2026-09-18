using EarlyEagles.Application.Abstractions.Persistence;
using EarlyEagles.Application.Behaviors;
using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-024.3 — the unit tests prove TransactionBehavior's control flow against a
// fake. These prove the EF Core implementation behind it actually commits and
// rolls back against real Postgres, which is the claim that matters: a command
// that throws must leave no trace. P0-049.3 builds directly on this to assert
// the same for outbox rows written by the dispatcher.
[Collection(PostgresCollectionDefinition.Name)]
public class EfUnitOfWorkTests
{
    private readonly PostgresFixture fixture;

    public EfUnitOfWorkTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Commit_Persists_Work_Done_Inside_The_Transaction()
    {
        var eventType = $"Committed_{Guid.NewGuid():N}";

        await using (var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var unitOfWork = new EfUnitOfWork(context);
            await using var transaction = await unitOfWork.BeginTransactionAsync(CancellationToken.None);

            context.OutboxMessages.Add(NewMessage(PostgresFixture.AlphaTenantId, eventType));
            await context.SaveChangesAsync();

            await transaction.CommitAsync(CancellationToken.None);
        }

        Assert.True(await ExistsAsync(eventType));
    }

    [Fact]
    public async Task Dispose_Without_Commit_Rolls_Back()
    {
        // The load-bearing assertion. TransactionBehavior never calls Rollback
        // explicitly — it relies on `await using` disposing an uncommitted
        // transaction. If EfTransaction.DisposeAsync didn't roll back, a failed
        // command would silently persist half its work.
        var eventType = $"RolledBack_{Guid.NewGuid():N}";

        await using (var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var unitOfWork = new EfUnitOfWork(context);
            await using var transaction = await unitOfWork.BeginTransactionAsync(CancellationToken.None);

            context.OutboxMessages.Add(NewMessage(PostgresFixture.AlphaTenantId, eventType));
            await context.SaveChangesAsync();

            // Leave the block without committing.
        }

        Assert.False(await ExistsAsync(eventType));
    }

    [Fact]
    public async Task Handler_Exception_Through_The_Behavior_Leaves_Nothing_Behind()
    {
        // End-to-end through the real behavior and the real unit of work: the
        // handler writes a row, then throws. Nothing should survive.
        var eventType = $"ThrewInHandler_{Guid.NewGuid():N}";
        var boom = new InvalidOperationException("handler failed after writing");

        await using (var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var behavior = new TransactionBehavior<SampleCommand, string>(new EfUnitOfWork(context));

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await behavior.Handle(
                    new SampleCommand(eventType),
                    async (_, ct) =>
                    {
                        context.OutboxMessages.Add(NewMessage(PostgresFixture.AlphaTenantId, eventType));
                        await context.SaveChangesAsync(ct);
                        throw boom;
                    },
                    CancellationToken.None));

            Assert.Same(boom, thrown);
        }

        Assert.False(await ExistsAsync(eventType));
    }

    [Fact]
    public async Task Successful_Command_Through_The_Behavior_Commits()
    {
        var eventType = $"HandlerSucceeded_{Guid.NewGuid():N}";

        await using (var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var behavior = new TransactionBehavior<SampleCommand, string>(new EfUnitOfWork(context));

            var response = await behavior.Handle(
                new SampleCommand(eventType),
                async (_, ct) =>
                {
                    context.OutboxMessages.Add(NewMessage(PostgresFixture.AlphaTenantId, eventType));
                    await context.SaveChangesAsync(ct);
                    return "ok";
                },
                CancellationToken.None);

            Assert.Equal("ok", response);
        }

        Assert.True(await ExistsAsync(eventType));
    }

    [Fact]
    public async Task Nested_Begin_Joins_The_Outer_Transaction_Instead_Of_Throwing()
    {
        // Npgsql has no nested transactions, so a command dispatched from inside
        // another command's handler would make EF throw on the second Begin.
        // The inner handle joins instead: its commit is a no-op, and the outer
        // transaction remains the sole decider.
        var eventType = $"Nested_{Guid.NewGuid():N}";

        await using (var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var unitOfWork = new EfUnitOfWork(context);
            await using var outer = await unitOfWork.BeginTransactionAsync(CancellationToken.None);

            await using (var inner = await unitOfWork.BeginTransactionAsync(CancellationToken.None))
            {
                context.OutboxMessages.Add(NewMessage(PostgresFixture.AlphaTenantId, eventType));
                await context.SaveChangesAsync();

                // The inner scope "commits" — but the outer one has not, so the
                // row must not be durable yet.
                await inner.CommitAsync(CancellationToken.None);
            }

            Assert.False(await ExistsAsync(eventType));

            await outer.CommitAsync(CancellationToken.None);
        }

        Assert.True(await ExistsAsync(eventType));
    }

    [Fact]
    public async Task Nested_Join_Is_Rolled_Back_When_The_Outer_Scope_Abandons()
    {
        // The mirror of the above: an inner scope that "succeeded" must still be
        // undone if the outer scope fails. Otherwise joining would silently
        // weaken atomicity rather than preserve it.
        var eventType = $"NestedRollback_{Guid.NewGuid():N}";

        await using (var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var unitOfWork = new EfUnitOfWork(context);
            await using var outer = await unitOfWork.BeginTransactionAsync(CancellationToken.None);

            await using (var inner = await unitOfWork.BeginTransactionAsync(CancellationToken.None))
            {
                context.OutboxMessages.Add(NewMessage(PostgresFixture.AlphaTenantId, eventType));
                await context.SaveChangesAsync();
                await inner.CommitAsync(CancellationToken.None);
            }

            // Outer never commits — leaves the block and disposes.
        }

        Assert.False(await ExistsAsync(eventType));
    }

    private static OutboxMessage NewMessage(Guid tenantId, string eventType) =>
        OutboxMessage.Create(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            eventType: eventType,
            payload: "{}",
            occurredAt: DateTimeOffset.UtcNow);

    // Reads on a separate context/connection, so it only sees committed rows —
    // reading through the same context would see its own uncommitted writes and
    // prove nothing.
    private async Task<bool> ExistsAsync(string eventType)
    {
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        return await context.OutboxMessages.AnyAsync(m => m.EventType == eventType);
    }

    private sealed record SampleCommand(string Value) : ICommand<string>;
}
