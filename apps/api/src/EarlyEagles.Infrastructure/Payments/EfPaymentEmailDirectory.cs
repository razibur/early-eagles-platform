using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Payments;

/// <summary>
/// EF Core <see cref="IPaymentEmailDirectory"/> (P1-010). Platform work, like
/// <c>EfBillingTenantDirectory</c>: it runs with no ambient tenant, so it opens
/// its own context, bypasses the query filter, and names the tenant in every
/// read itself.
/// </summary>
public sealed class EfPaymentEmailDirectory : IPaymentEmailDirectory
{
    private readonly DbContextOptions<ApplicationDbContext> options;

    public EfPaymentEmailDirectory(DbContextOptions<ApplicationDbContext> options)
    {
        this.options = options;
    }

    public async Task<PaymentEmailFacts?> FindAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken)
    {
        await using var context = new ApplicationDbContext(options, new TenantContext());

        var payment = await context.SessionPayments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == paymentId, cancellationToken)
            .ConfigureAwait(false);
        if (payment is null)
        {
            return null;
        }

        var session = await context.Sessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(s => s.TenantId == tenantId && s.Id == payment.SessionId, cancellationToken)
            .ConfigureAwait(false);

        var clubName = await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.DisplayName)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var adminEmails = await context.TenantMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Role == TenantMemberRole.TenantAdmin && m.Email != null)
            .Select(m => m.Email!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var memberName = await context.TenantMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ClerkUserId == payment.ClerkUserId)
            .Select(m => m.DisplayName)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaymentEmailFacts(clubName, adminEmails, memberName, session.Date, session.Venue, payment.AmountCents, payment.SenderName);
    }
}
