using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Payments;

/// <summary>A member says they paid for a session, first time or after a rejection (P1-001.3).</summary>
public sealed record PaymentReported(Guid TenantId, Guid SessionId, Guid PaymentId, string ClerkUserId, int AmountCents) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
