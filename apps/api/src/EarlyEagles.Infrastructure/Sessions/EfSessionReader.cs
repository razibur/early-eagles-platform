using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Sessions;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Sessions;

/// <summary>
/// EF <see cref="ISessionReader"/>. Reads through the request's context, so the
/// tenant query filter scopes every result; there is deliberately no tenant
/// parameter here.
/// </summary>
public sealed class EfSessionReader : ISessionReader
{
    private readonly ApplicationDbContext context;

    public EfSessionReader(ApplicationDbContext context)
    {
        this.context = context;
    }

    public async Task<IReadOnlyList<SessionSummary>> ListAsync(DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken) =>
        await context.Sessions
            .AsNoTracking()
            .Where(s => s.Date >= firstDay && s.Date <= lastDay)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .Select(s => new SessionSummary(s.Id, s.Date, s.StartTime, s.Venue, s.FeeCents, s.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
