using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.Common;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Sessions;
using Mediator;

namespace EarlyEagles.Application.Payments;

public sealed class ReportPaymentCommandHandler : ICommandHandler<ReportPaymentCommand, ReportPaymentResult>
{
    private readonly ISessionRepository sessions;
    private readonly ISessionPaymentRepository payments;
    private readonly ITenantContext tenantContext;
    private readonly TimeProvider timeProvider;

    public ReportPaymentCommandHandler(
        ISessionRepository sessions,
        ISessionPaymentRepository payments,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        this.sessions = sessions;
        this.payments = payments;
        this.tenantContext = tenantContext;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<ReportPaymentResult> Handle(ReportPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await sessions.FindAsync(command.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Session not found.");

        if (session.Status is SessionStatus.Cancelled)
        {
            throw new ConflictException("This session was cancelled, so there is nothing to pay.");
        }

        if (session.FeeCents == 0)
        {
            throw new ConflictException("This session is free, so there is nothing to pay.");
        }

        var now = timeProvider.GetUtcNow();
        var payment = await payments.FindAsync(command.SessionId, command.ClerkUserId, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            payment = SessionPayment.Report(
                Guid.CreateVersion7(),
                tenantContext.TenantId,
                session.Id,
                command.ClerkUserId,
                session.FeeCents,
                command.SenderName,
                command.Reference,
                now);
            payments.Add(payment);
        }
        else if (payment.Status is PaymentStatus.Rejected)
        {
            payment.ReportAgain(session.FeeCents, command.SenderName, command.Reference, now);
        }
        else
        {
            throw new ConflictException(payment.Status is PaymentStatus.Confirmed
                ? "Your payment for this session is already confirmed."
                : "Your payment for this session is already waiting for an admin.");
        }

        await payments.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ReportPaymentResult(payment.Id);
    }
}
