using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Payments;

/// <summary>EF Core <see cref="ISessionPaymentRepository"/> on the request's context.</summary>
public sealed class EfSessionPaymentRepository : ISessionPaymentRepository
{
    private readonly ApplicationDbContext context;

    public EfSessionPaymentRepository(ApplicationDbContext context)
    {
        this.context = context;
    }

    public Task<SessionPayment?> FindAsync(Guid sessionId, string clerkUserId, CancellationToken cancellationToken)
    {
        // A context created before the tenant resolved filters on no tenant and
        // would report every payment as missing, inviting a duplicate.
        if (context.CurrentTenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The request's DbContext has no tenant. Resolve the tenant before anything in the request uses ApplicationDbContext.");
        }

        return context.SessionPayments.SingleOrDefaultAsync(p => p.SessionId == sessionId && p.ClerkUserId == clerkUserId, cancellationToken);
    }

    public void Add(SessionPayment payment) => context.SessionPayments.Add(payment);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
