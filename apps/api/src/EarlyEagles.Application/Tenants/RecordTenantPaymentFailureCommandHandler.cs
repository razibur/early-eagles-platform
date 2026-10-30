using EarlyEagles.Application.Abstractions.Tenancy;
using Mediator;

namespace EarlyEagles.Application.Tenants;

public sealed class RecordTenantPaymentFailureCommandHandler : ICommandHandler<RecordTenantPaymentFailureCommand, bool>
{
    private readonly ITenantRepository tenants;
    private readonly ITenantContext tenantContext;

    public RecordTenantPaymentFailureCommandHandler(ITenantRepository tenants, ITenantContext tenantContext)
    {
        this.tenants = tenants;
        this.tenantContext = tenantContext;
    }

    public async ValueTask<bool> Handle(RecordTenantPaymentFailureCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The audit row must be filed under the tenant whose billing changed.
        if (!tenantContext.IsResolved || tenantContext.TenantId != command.TenantId)
        {
            throw new InvalidOperationException(
                "RecordTenantPaymentFailureCommand must run with the ambient tenant set to the tenant it bills.");
        }

        var tenant = await tenants.FindAsync(command.TenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {command.TenantId} does not exist.");

        if (!tenant.RecordPaymentFailure(command.StripeInvoiceId))
        {
            return false;
        }

        // SaveChanges drains PaymentFailed into the outbox, in this command's
        // transaction.
        await tenants.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
