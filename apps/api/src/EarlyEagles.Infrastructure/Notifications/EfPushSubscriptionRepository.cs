using EarlyEagles.Application.Abstractions.Notifications;
using EarlyEagles.Domain.Notifications;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Notifications;

/// <summary>
/// EF Core <see cref="IPushSubscriptionRepository"/>. Uses the request's
/// context, so the tenant query filter and the write-side stamping interceptor
/// both apply.
/// </summary>
public sealed class EfPushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly ApplicationDbContext context;

    public EfPushSubscriptionRepository(ApplicationDbContext context)
    {
        this.context = context;
    }

    public Task<PushSubscription?> FindByEndpointAsync(Guid tenantId, string endpoint, CancellationToken cancellationToken)
    {
        // Same guard as EfTenantMemberRepository: a context created before the
        // tenant resolved would hide the existing row, and the insert that
        // follows would hit the unique index.
        if (context.CurrentTenantId != tenantId)
        {
            throw new InvalidOperationException(
                $"The request's DbContext was created for tenant '{context.CurrentTenantId}', not '{tenantId}'. " +
                "Resolve the tenant before anything in the request uses ApplicationDbContext.");
        }

        return context.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == endpoint, cancellationToken);
    }

    public void Add(PushSubscription subscription) => context.PushSubscriptions.Add(subscription);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
