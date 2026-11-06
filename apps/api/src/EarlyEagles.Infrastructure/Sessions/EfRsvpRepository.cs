using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Common;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A double tap: two first answers raced and the unique index kept one.
            throw new ConflictException("Your answer was just saved by another request.");
        }
    }
}
