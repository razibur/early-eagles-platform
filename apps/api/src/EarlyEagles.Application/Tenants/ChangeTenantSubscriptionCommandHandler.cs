using EarlyEagles.Application.Abstractions.Tenancy;
using Mediator;

namespace EarlyEagles.Application.Tenants;

public sealed class ChangeTenantSubscriptionCommandHandler : ICommandHandler<ChangeTenantSubscriptionCommand, bool>
{
    private readonly ITenantRepository tenants;
    private readonly ITenantContext tenantContext;

    public ChangeTenantSubscriptionCommandHandler(ITenantRepository tenants, ITenantContext tenantContext)
    {
        this.tenants = tenants;
        this.tenantContext = tenantContext;
    }

    public async ValueTask<bool> Handle(ChangeTenantSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The audit row must be filed under the tenant whose billing changed.
        if (!tenantContext.IsResolved || tenantContext.TenantId != command.TenantId)
        {
            throw new InvalidOperationException(
                "ChangeTenantSubscriptionCommand must run with the ambient tenant set to the tenant it bills.");
        }

        var tenant = await tenants.FindAsync(command.TenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {command.TenantId} does not exist.");

        var changed = command.Change switch
        {
            TenantSubscriptionChange.Reactivate => tenant.ReactivateSubscription(command.StripeSubscriptionId),
            TenantSubscriptionChange.Suspend => tenant.SuspendSubscription(command.StripeSubscriptionId),
            TenantSubscriptionChange.End => tenant.EndSubscription(command.StripeSubscriptionId),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Change, "Unknown subscription change."),
        };

        if (!changed)
        {
            return false;
        }

        // SaveChanges drains the event into the outbox, in this command's
        // transaction.
        await tenants.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
