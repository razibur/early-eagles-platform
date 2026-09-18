using EarlyEagles.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EarlyEagles.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>, backing
/// <c>TransactionBehavior</c> (P0-024.3). Wraps the scoped
/// <see cref="ApplicationDbContext"/>'s connection in a real database
/// transaction so a command's state changes — and, from P0-049.2, the outbox
/// rows describing them — commit or roll back as one unit.
/// </summary>
/// <remarks>
/// <para>
/// Scoped, matching the context it wraps: one request, one connection, one
/// transaction.
/// </para>
/// <para>
/// If a transaction is already open on the context, <see cref="BeginTransactionAsync"/>
/// returns a handle that <em>joins</em> it rather than opening a second one
/// (Npgsql has no nested transactions, and EF would throw). The joined handle's
/// commit is a no-op and its dispose leaves the outer transaction alone, so the
/// outermost scope stays the sole owner: an inner failure still propagates and
/// rolls the whole thing back, and an inner success never commits work the
/// outer scope may yet abandon. This keeps a command dispatched from inside
/// another command's handler from breaking the pipeline.
/// </para>
/// <para>
/// The audit log deliberately does <em>not</em> participate: <c>AuditService</c>
/// opens its own context on a separate connection precisely so a rolled-back
/// command still leaves a trail (P0-047.1).
/// </para>
/// </remarks>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext context;

    public EfUnitOfWork(ApplicationDbContext context)
    {
        this.context = context;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            return new JoinedTransaction();
        }

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransaction(transaction);
    }

    /// <summary>
    /// Owns the underlying provider transaction. Disposing without a prior
    /// commit rolls back, which is what <see cref="IUnitOfWorkTransaction"/>
    /// promises and what <c>TransactionBehavior</c>'s <c>await using</c> relies
    /// on when a handler throws.
    /// </summary>
    private sealed class EfTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction transaction;

        public EfTransaction(IDbContextTransaction transaction)
        {
            this.transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            return transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// A participant in a transaction someone else owns. Both operations are
    /// no-ops so the owner decides the outcome.
    /// </summary>
    private sealed class JoinedTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
