namespace EarlyEagles.Application.Abstractions.Payments;

/// <summary>
/// Looks up a payment for an outbox consumer (P1-010). A consumer runs outside
/// any request, so there is no ambient tenant: the tenant is an explicit
/// argument and implementations must scope every read to it.
/// </summary>
public interface IPaymentEmailDirectory
{
    /// <summary>The email's facts; <c>null</c> when the tenant has no such payment.</summary>
    Task<PaymentEmailFacts?> FindAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken);
}
