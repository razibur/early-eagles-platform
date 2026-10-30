using EarlyEagles.Application.Abstractions.Tenancy;
using Mediator;

namespace EarlyEagles.Application.Tenants;

public sealed class StartTenantSubscriptionCommandHandler : ICommandHandler<StartTenantSubscriptionCommand, bool>
{
    private readonly ITenantRepository tenants;
    private readonly ITenantContext tenantContext;

    public StartTenantSubscriptionCommandHandler(ITenantRepository tenants, ITenantContext tenantContext)
    {
        this.tenants = tenants;
        this.tenantContext = tenantContext;
    }

    public async ValueTask<bool> Handle(StartTenantSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // As for provisioning: the audit row must be filed under the tenant
        // whose billing changed.
        if (!tenantContext.IsResolved || tenantContext.TenantId != command.TenantId)
        {
            throw new InvalidOperationException(
                "StartTenantSubscriptionCommand must run with the ambient tenant set to the tenant it bills.");
        }

        var tenant = await tenants.FindAsync(command.TenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {command.TenantId} does not exist.");

        if (!tenant.LinkStripeSubscription(command.StripeCustomerId, command.StripeSubscriptionId))
        {
            return false;
        }

        // SaveChanges drains SubscriptionStarted into the outbox, in this
        // command's transaction.
        await tenants.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
