namespace EarlyEagles.Application.Abstractions.Persistence;

/// <summary>
/// A scoped database transaction obtained from <see cref="IUnitOfWork.BeginTransactionAsync"/>.
/// Call <see cref="CommitAsync"/> on success; disposing without committing causes the
/// underlying provider to roll back. Implementations are <see cref="IAsyncDisposable"/>
/// so they integrate cleanly with <c>await using</c> blocks.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
