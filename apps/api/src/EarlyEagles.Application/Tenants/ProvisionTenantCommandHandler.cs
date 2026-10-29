using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using Mediator;

namespace EarlyEagles.Application.Tenants;

public sealed class ProvisionTenantCommandHandler : ICommandHandler<ProvisionTenantCommand, ProvisionTenantResult>
{
    private readonly ITenantRepository tenants;
    private readonly ITenantContext tenantContext;

    public ProvisionTenantCommandHandler(ITenantRepository tenants, ITenantContext tenantContext)
    {
        this.tenants = tenants;
        this.tenantContext = tenantContext;
    }

    public async ValueTask<ProvisionTenantResult> Handle(ProvisionTenantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The tenant written must be the ambient one, or the audit row would be
        // filed under a different tenant than the change it records.
        if (!tenantContext.IsResolved || tenantContext.TenantId != command.TenantId)
        {
            throw new InvalidOperationException(
                "ProvisionTenantCommand must run with the ambient tenant set to the tenant it provisions.");
        }

        var tenant = await tenants.FindAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        ProvisionTenantResult result;
        if (tenant is null)
        {
            tenant = Tenant.Create(command.TenantId, command.Slug, command.DisplayName);
            tenant.LinkClerkOrganization(command.ClerkOrganizationId);
            tenants.Add(tenant);
            result = ProvisionTenantResult.Created;
        }
        else
        {
            // A redelivery, or the organization renamed before we saw it. The
            // slug is the tenant's address and stays as first provisioned.
            tenant.LinkClerkOrganization(command.ClerkOrganizationId);
            if (tenant.DisplayName != command.DisplayName.Trim())
            {
                tenant.Rename(command.DisplayName);
            }

            result = ProvisionTenantResult.Updated;
        }

        await tenants.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
