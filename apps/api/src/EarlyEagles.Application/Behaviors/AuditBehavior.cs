using EarlyEagles.Application.Abstractions.Auditing;
using Mediator;

namespace EarlyEagles.Application.Behaviors;

/// <summary>
/// Mediator pipeline behavior that records an <see cref="AuditEntry"/> for every
/// <see cref="ICommand"/> dispatched through the mediator — both successful
/// outcomes and failures. Queries (<see cref="IQuery{TResponse}"/>) and plain
/// <see cref="IRequest{TResponse}"/> messages pass through transparently.
/// Mediator's built-in <see cref="ICommand"/> and <see cref="IQuery{TResponse}"/>
/// markers give us free CQRS-style gating without rolling our own.
///
/// Sits OUTSIDE <see cref="TransactionBehavior{TMessage, TResponse}"/> in the
/// pipeline. The <see cref="IAuditService"/> implementation writes audit rows in
/// a transaction independent of the request transaction (P0-047), so a rolled-
/// back command still leaves an audit trail showing the attempt + failure
/// reason. That separation is the entire point of the audit log.
///
/// Pipeline order (when fully wired):
///   Logging → Audit → Validation → Transaction → handler
///
/// NOTE: this behavior is NOT registered in <c>AddApplication</c> yet — the
/// concrete <see cref="IAuditService"/> implementation lands with P0-047.1.
/// </summary>
public sealed class AuditBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    // Mediator's non-generic ICommand and generic ICommand<TResponse> are SEPARATE
    // interfaces — implementing ICommand<string> does NOT also implement ICommand.
    // Cache the "is this message type a command" decision once per closed generic
    // so the per-request cost is a single field read.
    private static readonly bool IsCommandMessage =
        typeof(ICommand).IsAssignableFrom(typeof(TMessage)) ||
        Array.Exists(
            typeof(TMessage).GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

    private readonly IAuditService auditService;

    public AuditBehavior(IAuditService auditService)
    {
        this.auditService = auditService;
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

        var action = typeof(TMessage).Name;

        try
        {
            var response = await next(message, cancellationToken).ConfigureAwait(false);
            await auditService
                .LogAsync(new AuditEntry(action, Success: true), cancellationToken)
                .ConfigureAwait(false);
            return response;
        }
        catch (OperationCanceledException)
        {
            // Cancellation isn't an audit-worthy outcome — propagate without logging
            // (logging would itself observe the cancelled token).
            throw;
        }
        catch (Exception ex)
        {
            await auditService
                .LogAsync(
                    new AuditEntry(action, Success: false, FailureReason: $"{ex.GetType().Name}: {ex.Message}"),
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }
}
