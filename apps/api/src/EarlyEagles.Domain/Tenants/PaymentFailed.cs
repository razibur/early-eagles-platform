using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// A Stripe invoice payment failed and the tenant went past due (P0-043.4).
/// Raised by <see cref="Tenant.RecordPaymentFailure"/> on the move from
/// active, never for a retry or a redelivery.
/// </summary>
public sealed record PaymentFailed(Guid TenantId, string StripeInvoiceId) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
