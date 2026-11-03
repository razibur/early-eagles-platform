using EarlyEagles.Application.Abstractions.Notifications;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Notifications;
using Mediator;

namespace EarlyEagles.Application.Notifications;

public sealed class SubscribeToPushCommandHandler : ICommandHandler<SubscribeToPushCommand, SubscribeToPushResult>
{
    private readonly IPushSubscriptionRepository subscriptions;
    private readonly ITenantContext tenantContext;
    private readonly TimeProvider timeProvider;

    public SubscribeToPushCommandHandler(IPushSubscriptionRepository subscriptions, ITenantContext tenantContext, TimeProvider timeProvider)
    {
        this.subscriptions = subscriptions;
        this.tenantContext = tenantContext;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<SubscribeToPushResult> Handle(SubscribeToPushCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // A subscription belongs to a club; without one there is nowhere to file it.
        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException("SubscribeToPushCommand must run with the ambient tenant resolved.");
        }

        var existing = await subscriptions
            .FindByEndpointAsync(tenantContext.TenantId, command.Endpoint, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (existing.Renew(command.ClerkUserId, command.P256dh, command.Auth))
            {
                await subscriptions.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return new SubscribeToPushResult(existing.Id, Created: false);
        }

        var subscription = PushSubscription.Create(
            Guid.CreateVersion7(),
            tenantContext.TenantId,
            command.ClerkUserId,
            command.Endpoint,
            command.P256dh,
            command.Auth,
            timeProvider.GetUtcNow());
        subscriptions.Add(subscription);
        await subscriptions.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SubscribeToPushResult(subscription.Id, Created: true);
    }
}
