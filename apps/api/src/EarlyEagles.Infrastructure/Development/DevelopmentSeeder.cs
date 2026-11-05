using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Development;

/// <summary>
/// Fills a local database with Tenant 0, an admin, sample members and next
/// Sunday's session (P1-004.1), so the API has something to show. Safe to run
/// again: it adds only what is missing.
/// </summary>
/// <remarks>
/// Development only, and never real people: the names and addresses are made up
/// and the real members arrive through Clerk (P1-004.2). The tenant isn't linked
/// to a Clerk organization, because that id belongs to an account.
/// </remarks>
public sealed class DevelopmentSeeder
{
    public const string TenantSlug = "early-eagles";
    public const string AdminClerkUserId = "user_dev_admin";
    public const int SampleMemberCount = 5;

    public static readonly Guid TenantId = new("e0000000-0000-0000-0000-000000000001");

    private readonly DbContextOptions<ApplicationDbContext> options;
    private readonly TimeProvider timeProvider;

    public DevelopmentSeeder(DbContextOptions<ApplicationDbContext> options, TimeProvider timeProvider)
    {
        this.options = options;
        this.timeProvider = timeProvider;
    }

    /// <summary>The first Sunday after <paramref name="today"/>; on a Sunday, the one a week on.</summary>
    public static DateOnly NextSunday(DateOnly today)
    {
        var days = (int)DayOfWeek.Sunday - (int)today.DayOfWeek;
        return today.AddDays(days <= 0 ? days + 7 : days);
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var tenantContext = new TenantContext();
        tenantContext.Initialize(TenantId, TenantSlug);
        await using var context = new ApplicationDbContext(options, tenantContext);

        if (!await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == TenantId, cancellationToken).ConfigureAwait(false))
        {
            context.Tenants.Add(Tenant.Create(TenantId, TenantSlug, "Early Eagles"));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var existing = await context.TenantMembers
            .Select(m => m.ClerkUserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var member in SampleMembers().Where(m => !existing.Contains(m.ClerkUserId)))
        {
            context.TenantMembers.Add(member);
        }

        var now = timeProvider.GetUtcNow();
        var sunday = NextSunday(DateOnly.FromDateTime(now.UtcDateTime));
        if (!await context.Sessions.AnyAsync(s => s.Date == sunday, cancellationToken).ConfigureAwait(false))
        {
            context.Sessions.Add(Session.Create(Guid.CreateVersion7(), TenantId, sunday, new TimeOnly(7, 0), "Sample Hall", Session.DefaultFeeCents, now));
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<TenantMember> SampleMembers()
    {
        var admin = TenantMember.Create(TenantId, AdminClerkUserId, TenantMemberRole.TenantAdmin);
        admin.UpdateProfile("Sample Admin", "admin@example.com", null);
        yield return admin;

        for (var i = 1; i <= SampleMemberCount; i++)
        {
            var member = TenantMember.Create(TenantId, $"user_dev_member_{i}", TenantMemberRole.Member);
            member.UpdateProfile($"Sample Member {i}", $"member{i}@example.com", null);
            yield return member;
        }
    }
}
