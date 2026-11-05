namespace EarlyEagles.Domain.Payments;

/// <summary>Where a member's reported payment stands (P1-001.3).</summary>
public enum PaymentStatus
{
    Reported,
    Confirmed,
    Rejected,
}
