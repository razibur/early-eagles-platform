using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using Mediator;

namespace EarlyEagles.Application.TenantMembers;

public sealed class AddTenantMemberCommandHandler : ICommandHandler<AddTenantMemberCommand, AddTenantMemberResult>
{
    private readonly ITenantMemberRepository members;
    private readonly ITenantContext tenantContext;

    public AddTenantMemberCommandHandler(ITenantMemberRepository members, ITenantContext tenantContext)
    {
        this.members = members;
        this.tenantContext = tenantContext;
    }

    public async ValueTask<AddTenantMemberResult> Handle(AddTenantMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The member written must belong to the ambient tenant, or the audit
        // row would be filed under a different tenant than the change it records.
        if (!tenantContext.IsResolved || tenantContext.TenantId != command.TenantId)
        {
            throw new InvalidOperationException(
                "AddTenantMemberCommand must run with the ambient tenant set to the member's tenant.");
        }

        var member = await members.FindAsync(command.TenantId, command.ClerkUserId, cancellationToken).ConfigureAwait(false);
        AddTenantMemberResult result;
        if (member is null)
        {
            members.Add(TenantMember.Create(command.TenantId, command.ClerkUserId, command.Role));
            result = AddTenantMemberResult.Created;
        }
        else
        {
            if (member.Role != command.Role)
            {
                member.ChangeRole(command.Role);
            }

            result = AddTenantMemberResult.Updated;
        }

        await members.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
