namespace EarlyEagles.Application.TenantMembers;

public enum AddTenantMemberResult
{
    /// <summary>A new member row was inserted.</summary>
    Created,

    /// <summary>The member existed; its role was brought in line with Clerk (or already was).</summary>
    Updated,
}
