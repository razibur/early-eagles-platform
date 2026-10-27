using EarlyEagles.Application.Abstractions.Auditing;
using EarlyEagles.Application.Auditing;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Auditing;

/// <summary>
/// EF implementation of <see cref="IAuditLogReader"/> (P0-048). Reads through
/// the request's <see cref="ApplicationDbContext"/>, so the global tenant
/// filter scopes every page; there is deliberately no tenant parameter here.
/// </summary>
/// <remarks>
/// The cursor condition is a Postgres row-value comparison,
/// <c>(occurred_at, id) &lt; (@t, @id)</c>, which matches the sort order exactly
/// and lets the <c>(tenant_id, occurred_at DESC)</c> index drive the scan.
/// Both the ordering and the comparison happen in SQL, so .NET's different
/// <see cref="Guid"/> sort order never comes into it.
/// </remarks>
public sealed class EfAuditLogReader : IAuditLogReader
{
    private readonly ApplicationDbContext db;

    public EfAuditLogReader(ApplicationDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<AuditEventSummary>> ListAsync(AuditEventCursor? after, int take, CancellationToken cancellationToken)
    {
        var events = db.AuditEvents.AsNoTracking();

        if (after is { } cursor)
        {
            events = events.Where(e => EF.Functions.LessThan(
                ValueTuple.Create(e.OccurredAt, e.Id),
                ValueTuple.Create(cursor.OccurredAt, cursor.Id)));
        }

        return await events
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(take)
            .Select(e => new AuditEventSummary(e.Id, e.Action, e.Success, e.FailureReason, e.ActorClerkUserId, e.OccurredAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
