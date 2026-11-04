using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Sessions;

/// <summary>
/// EF Core <see cref="ISessionRepository"/>. Uses the request's context, so the
/// tenant query filter and the write-side stamping interceptor both apply: a
/// lookup of another club's session id finds nothing.
/// </summary>
public sealed class EfSessionRepository : ISessionRepository
{
    private readonly ApplicationDbContext context;

    public EfSessionRepository(ApplicationDbContext context)
    {
        this.context = context;
    }

    public Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // A context created before the tenant resolved filters on no tenant and
        // would report every session as missing.
        if (context.CurrentTenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The request's DbContext has no tenant. Resolve the tenant before anything in the request uses ApplicationDbContext.");
        }

        return context.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    public void Add(Session session) => context.Sessions.Add(session);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
