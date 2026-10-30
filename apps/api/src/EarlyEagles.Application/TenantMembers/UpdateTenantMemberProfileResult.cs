namespace EarlyEagles.Application.TenantMembers;

public enum UpdateTenantMemberProfileResult
{
    /// <summary>The profile changed.</summary>
    Updated,

    /// <summary>The profile already matched Clerk; nothing was written.</summary>
    Unchanged,

    /// <summary>The user isn't a member of the tenant (left between the lookup and the command).</summary>
    NotAMember,
}
