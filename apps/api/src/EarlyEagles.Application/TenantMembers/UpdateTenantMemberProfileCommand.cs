using Mediator;

namespace EarlyEagles.Application.TenantMembers;

/// <summary>
/// Copies a Clerk user's profile onto their member row in one tenant
/// (P0-036.5.1). Clerk's <c>user.updated</c> sends one of these per tenant the
/// user belongs to.
/// </summary>
/// <remarks>
/// The caller sets the ambient tenant to <see cref="TenantId"/> first, so the
/// audit row lands in that tenant's log. Clerk sends the whole user, so the
/// whole profile is replaced (see <c>TenantMember.UpdateProfile</c>).
/// </remarks>
public sealed record UpdateTenantMemberProfileCommand(
    Guid TenantId,
    string ClerkUserId,
    string? DisplayName,
    string? Email,
    string? ImageUrl) : ICommand<UpdateTenantMemberProfileResult>;
