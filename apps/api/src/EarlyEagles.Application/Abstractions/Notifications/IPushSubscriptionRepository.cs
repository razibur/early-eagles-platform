using EarlyEagles.Domain.Notifications;

namespace EarlyEagles.Application.Abstractions.Notifications;

/// <summary>
/// Write side for <see cref="PushSubscription"/> (P0-068.3). Reads go through
/// the tenant query filter, so only the ambient tenant's rows are visible;
/// saving runs inside the command's transaction.
/// </summary>
public interface IPushSubscriptionRepository
{
    /// <summary>The ambient tenant's subscription for this endpoint, tracked for update; <c>null</c> when there is none.</summary>
    Task<PushSubscription?> FindByEndpointAsync(Guid tenantId, string endpoint, CancellationToken cancellationToken);

    void Add(PushSubscription subscription);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
