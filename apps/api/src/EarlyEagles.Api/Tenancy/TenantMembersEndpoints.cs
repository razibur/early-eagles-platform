using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Api.Tenancy;

/// <summary>
/// Read endpoints over <c>tenant_members</c>. The primary purpose today is to
/// exercise the isolation gate (P0-034.6): a lookup against the ambient tenant
/// naturally returns null for another tenant's row because the global query
/// filter (P0-032.2) is invisible to the handler. When Clerk webhooks land
/// (P0-036.*) and real admin views need this data, the endpoint moves behind a
/// Mediator query + Application-layer reader; today it reads
/// <see cref="ApplicationDbContext"/> directly to keep the isolation proof
/// dependency-free.
/// </summary>
public static class TenantMembersEndpoints
{
    public static IEndpointRouteBuilder MapTenantMembersEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapGet("/tenant-members/{clerkUserId}", GetByClerkUserId)
            .WithName("GetTenantMemberByClerkUserId")
            .WithTags("TenantMembers");

        return routes;
    }

    private static async Task<Results<Ok<TenantMemberResponse>, NotFound, ProblemHttpResult>> GetByClerkUserId(
        string clerkUserId,
        ITenantContext tenant,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        if (!tenant.IsResolved)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Tenant not resolved",
                detail: "Requests must target a tenant subdomain, e.g. '<tenant>.earlyeagles.club'.");
        }

        // The global query filter appends `WHERE tenant_id = @currentTenantId`, so a
        // Beta member's ClerkUserId requested by Alpha materialises as null — and
        // the endpoint maps that to 404 the same way it does for a truly missing id.
        // A caller cannot tell the two apart, which is the whole point.
        var member = await db.TenantMembers
            .FirstOrDefaultAsync(m => m.ClerkUserId == clerkUserId, cancellationToken);

        if (member is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new TenantMemberResponse(member.TenantId, member.ClerkUserId, member.Role));
    }
}
