namespace EarlyEagles.Application.Tenants;

/// <summary>What a Stripe subscription event does to its tenant (P0-043.3).</summary>
public enum TenantSubscriptionChange
{
    /// <summary>The subscription is active again: a past-due or suspended tenant becomes active.</summary>
    Reactivate = 0,

    /// <summary>Stripe stopped collecting (unpaid or paused): the tenant is suspended.</summary>
    Suspend = 1,

    /// <summary>The subscription ended: the tenant is back on the free plan.</summary>
    End = 2,
}
