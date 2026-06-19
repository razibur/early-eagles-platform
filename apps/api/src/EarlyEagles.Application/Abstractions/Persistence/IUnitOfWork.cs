namespace EarlyEagles.Application.Abstractions.Persistence;

/// <summary>
/// Application-layer port for transactional persistence. The EF Core implementation
/// (lives in <c>EarlyEagles.Infrastructure</c>, lands with P0-032.2) wraps the
/// <c>DbContext</c>'s <c>Database.BeginTransactionAsync</c>. Application handlers
/// never see EF Core directly — they depend on this interface.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begins a database transaction. The returned <see cref="IUnitOfWorkTransaction"/>
    /// must be either committed or disposed. Disposing without a commit rolls back.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
