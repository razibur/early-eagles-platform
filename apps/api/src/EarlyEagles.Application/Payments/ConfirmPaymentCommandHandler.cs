using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Application.Common;
using EarlyEagles.Domain.Payments;
using Mediator;

namespace EarlyEagles.Application.Payments;

public sealed class ConfirmPaymentCommandHandler : ICommandHandler<ConfirmPaymentCommand>
{
    private readonly ISessionPaymentRepository payments;
    private readonly TimeProvider timeProvider;

    public ConfirmPaymentCommandHandler(ISessionPaymentRepository payments, TimeProvider timeProvider)
    {
        this.payments = payments;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<Unit> Handle(ConfirmPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = await payments.FindAsync(command.SessionId, command.MemberClerkUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Payment not found.");

        if (payment.Status is PaymentStatus.Rejected)
        {
            throw new ConflictException("This payment was rejected. The member needs to report it again.");
        }

        if (payment.Confirm(command.ReviewedBy, timeProvider.GetUtcNow()))
        {
            await payments.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
