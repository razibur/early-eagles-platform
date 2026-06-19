using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace EarlyEagles.Application.Behaviors;

/// <summary>
/// Mediator pipeline behavior that runs every FluentValidation
/// <see cref="IValidator{T}"/> registered for the current message type before
/// the handler executes. If any validator produces failures, the behavior throws
/// <see cref="ValidationException"/> with the combined failures and the handler
/// is never invoked. If no validators are registered for the message type, the
/// behavior is transparent.
///
/// Sits after <see cref="LoggingBehavior{TMessage, TResponse}"/> in the pipeline
/// so the enter / fail logs still capture the validation outcome via the error
/// log of the outer behavior.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    private readonly IEnumerable<IValidator<TMessage>> validators;

    public ValidationBehavior(IEnumerable<IValidator<TMessage>> validators)
    {
        this.validators = validators;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var validatorList = validators as IList<IValidator<TMessage>> ?? [.. validators];
        if (validatorList.Count == 0)
        {
            return await next(message, cancellationToken).ConfigureAwait(false);
        }

        var context = new ValidationContext<TMessage>(message);
        var validationTasks = validatorList.Select(v => v.ValidateAsync(context, cancellationToken));
        var results = await Task.WhenAll(validationTasks).ConfigureAwait(false);

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(message, cancellationToken).ConfigureAwait(false);
    }
}
