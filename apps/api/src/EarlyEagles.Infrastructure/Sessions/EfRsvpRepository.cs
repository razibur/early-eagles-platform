using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Sessions;

/// <summary>EF Core <see cref="IRsvpRepository"/> on the request's context.</summary>
public sealed class EfRsvpRepository : IRsvpRepository
{
    private readonly ApplicationDbContext context;

    public EfRsvpRepository(ApplicationDbContext context)
    {
        this.context = context;
    }

    public Task<Rsvp?> FindAsync(Guid sessionId, string clerkUserId, CancellationToken cancellationToken)
    {
        // A context created before the tenant resolved filters on no tenant and
        // would report every answer as missing, inviting a duplicate.
        if (context.CurrentTenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The request's DbContext has no tenant. Resolve the tenant before anything in the request uses ApplicationDbContext.");
        }

        return context.Rsvps.SingleOrDefaultAsync(r => r.SessionId == sessionId && r.ClerkUserId == clerkUserId, cancellationToken);
    }

    public void Add(Rsvp rsvp) => context.Rsvps.Add(rsvp);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
