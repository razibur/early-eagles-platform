using EarlyEagles.Application.Abstractions.Persistence;
using Mediator;

namespace EarlyEagles.Application.Behaviors;

/// <summary>
/// Mediator pipeline behavior that wraps every <see cref="ICommand"/> in a
/// database transaction. The transaction begins before the handler runs; on
/// success it commits, on exception it is disposed without committing (which
/// rolls back per <see cref="IUnitOfWorkTransaction"/>'s contract). Together
/// with the outbox dispatcher (P0-049 / P0-050) this guarantees state changes
/// and the domain events that describe them land atomically.
///
/// Queries (<see cref="IQuery{TResponse}"/>) and plain
/// <see cref="IRequest{TResponse}"/> messages pass through untouched — wrapping
/// a read in an explicit transaction buys nothing and costs a round trip. Same
/// <see cref="ICommand"/> gate as <see cref="AuditBehavior{TMessage, TResponse}"/>.
///
/// Innermost of the registered behaviors, so:
///   Logging → Audit → Validation → Transaction → handler
/// A validation failure throws before any transaction is opened, and the audit
/// row is written outside the transaction that just rolled back — which is the
/// entire point of auditing failures (see <see cref="AuditBehavior{TMessage, TResponse}"/>).
/// </summary>
public sealed class TransactionBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    // Mediator's non-generic ICommand and generic ICommand<TResponse> are SEPARATE
    // interfaces — implementing ICommand<string> does NOT also implement ICommand.
    // Cache the decision once per closed generic so the per-request cost is a
    // single field read.
    private static readonly bool IsCommandMessage =
        typeof(ICommand).IsAssignableFrom(typeof(TMessage)) ||
        Array.Exists(
            typeof(TMessage).GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

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

        if (!IsCommandMessage)
        {
            return await next(message, cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await unitOfWork
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var response = await next(message, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }
}
