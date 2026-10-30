using EarlyEagles.Application.Abstractions.Tenancy;
using Mediator;

namespace EarlyEagles.Application.TenantMembers;

public sealed class UpdateTenantMemberProfileCommandHandler
    : ICommandHandler<UpdateTenantMemberProfileCommand, UpdateTenantMemberProfileResult>
{
    private readonly ITenantMemberRepository members;
    private readonly ITenantContext tenantContext;

    public UpdateTenantMemberProfileCommandHandler(ITenantMemberRepository members, ITenantContext tenantContext)
    {
        this.members = members;
        this.tenantContext = tenantContext;
    }

    public async ValueTask<UpdateTenantMemberProfileResult> Handle(
        UpdateTenantMemberProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Same rule as AddTenantMemberCommand: the audit row must be filed
        // under the tenant whose member changed.
        if (!tenantContext.IsResolved || tenantContext.TenantId != command.TenantId)
        {
            throw new InvalidOperationException(
                "UpdateTenantMemberProfileCommand must run with the ambient tenant set to the member's tenant.");
        }

        var member = await members.FindAsync(command.TenantId, command.ClerkUserId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return UpdateTenantMemberProfileResult.NotAMember;
        }

        if (!member.UpdateProfile(command.DisplayName, command.Email, command.ImageUrl))
        {
            return UpdateTenantMemberProfileResult.Unchanged;
        }

        await members.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return UpdateTenantMemberProfileResult.Updated;
    }
}
