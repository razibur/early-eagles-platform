using System.Security.Claims;
using System.Text.Json;

namespace EarlyEagles.Api.Authentication;

/// <summary>
/// The organization a Clerk session token says is active (P0-035.3): the
/// club the user is acting in, and their role there.
/// </summary>
/// <param name="Id">The Clerk organization id, <c>org_…</c>.</param>
/// <param name="Slug">The organization's slug, when Clerk sent one.</param>
/// <param name="Role">The role key with its <c>org:</c> prefix (<c>org:admin</c>), as webhooks send it.</param>
public sealed record ClerkActiveOrganization(string Id, string? Slug, string? Role)
{
    private const string RolePrefix = "org:";

    /// <summary>
    /// Reads the active organization from a v2 token's <c>o</c> claim
    /// (<c>{ id, slg, rol }</c>, role without its prefix), falling back to v1's
    /// <c>org_id</c> / <c>org_slug</c> / <c>org_role</c>. Null when no
    /// organization is active or the claim is unreadable.
    /// </summary>
    public static ClerkActiveOrganization? From(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var compact = user.FindFirst(ClerkClaims.Organization)?.Value;
        if (compact is not null)
        {
            return FromCompact(compact);
        }

        var id = user.FindFirst(ClerkClaims.LegacyOrganizationId)?.Value;
        return string.IsNullOrEmpty(id)
            ? null
            : new ClerkActiveOrganization(id, user.FindFirst(ClerkClaims.LegacyOrganizationSlug)?.Value, user.FindFirst(ClerkClaims.LegacyOrganizationRole)?.Value);
    }

    private static ClerkActiveOrganization? FromCompact(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var id = StringProperty(root, "id");
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var role = StringProperty(root, "rol");
            if (!string.IsNullOrEmpty(role) && !role.StartsWith(RolePrefix, StringComparison.Ordinal))
            {
                role = RolePrefix + role;
            }

            return new ClerkActiveOrganization(id, StringProperty(root, "slg"), string.IsNullOrEmpty(role) ? null : role);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
