using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Application.Common;
using EarlyEagles.Domain.Payments;
using Mediator;

namespace EarlyEagles.Application.Payments;

public sealed class RejectPaymentCommandHandler : ICommandHandler<RejectPaymentCommand>
{
    private readonly ISessionPaymentRepository payments;
    private readonly TimeProvider timeProvider;

    public RejectPaymentCommandHandler(ISessionPaymentRepository payments, TimeProvider timeProvider)
    {
        this.payments = payments;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<Unit> Handle(RejectPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = await payments.FindAsync(command.SessionId, command.MemberClerkUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Payment not found.");

        if (payment.Status is PaymentStatus.Confirmed)
        {
            throw new ConflictException("This payment is already confirmed.");
        }

        if (payment.Reject(command.ReviewedBy, command.Reason, timeProvider.GetUtcNow()))
        {
            await payments.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
