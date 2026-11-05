using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Payments;

/// <summary>An admin saw the money arrive (P1-001.3). Raised once per payment.</summary>
public sealed record PaymentConfirmed(Guid TenantId, Guid SessionId, Guid PaymentId, string ClerkUserId, int AmountCents) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
