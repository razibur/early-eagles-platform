using EarlyEagles.Application.Abstractions.Persistence;
using Mediator;

namespace EarlyEagles.Application.Behaviors;

/// <summary>
/// Mediator pipeline behavior that wraps every request in a database transaction.
/// The transaction begins before the handler runs; on success it commits, on
/// exception it is disposed without committing (which rolls back per
/// <see cref="IUnitOfWorkTransaction"/>'s contract). Together with the outbox
/// dispatcher (P0-049 / P0-050) this guarantees state changes and the domain
/// events that describe them land atomically.
///
/// Runs after <see cref="LoggingBehavior{TMessage, TResponse}"/> and
/// <see cref="ValidationBehavior{TMessage, TResponse}"/> in the pipeline — a
/// validation failure must throw before any transaction is opened.
///
/// NOTE: this behavior is NOT registered in <c>AddApplication</c> yet — the
/// concrete <see cref="IUnitOfWork"/> implementation lands with P0-032.2
/// (DbContext + EF Core). Pipeline registration moves to that commit.
/// </summary>
public sealed class TransactionBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    private readonly IUnitOfWork unitOfWork;

    public TransactionBehavior(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        await using var transaction = await unitOfWork
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var response = await next(message, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }
}
