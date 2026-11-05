using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Sessions;
using EarlyEagles.Domain.Sessions;
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

    public async Task<SessionDetails?> GetDetailsAsync(Guid sessionId, string clerkUserId, CancellationToken cancellationToken)
    {
        var session = await context.Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        var answers = await context.Rsvps
            .AsNoTracking()
            .Where(r => r.SessionId == sessionId)
            .Select(r => new { r.ClerkUserId, r.Response })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var payment = await context.SessionPayments
            .AsNoTracking()
            .Where(p => p.SessionId == sessionId && p.ClerkUserId == clerkUserId)
            .Select(p => new MyPayment(p.Status, p.AmountCents, p.RejectionReason))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SessionDetails(
            session.Id,
            session.Date,
            session.StartTime,
            session.Venue,
            session.FeeCents,
            session.Status,
            new RsvpCounts(
                answers.Count(a => a.Response is RsvpResponse.Yes),
                answers.Count(a => a.Response is RsvpResponse.No),
                answers.Count(a => a.Response is RsvpResponse.Maybe)),
            answers.SingleOrDefault(a => a.ClerkUserId == clerkUserId)?.Response,
            payment);
    }

    public Task<bool> ExistsAsync(Guid sessionId, CancellationToken cancellationToken) =>
        context.Sessions.AnyAsync(s => s.Id == sessionId, cancellationToken);

    public async Task<IReadOnlyList<RsvpGridRow>> ListRsvpGridAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await context.TenantMembers
            .AsNoTracking()
            .OrderBy(m => m.DisplayName == null)
            .ThenBy(m => m.DisplayName)
            .ThenBy(m => m.ClerkUserId)
            .Select(m => new RsvpGridRow(
                m.ClerkUserId,
                m.DisplayName,
                m.ImageUrl,
                context.Rsvps
                    .Where(r => r.SessionId == sessionId && r.ClerkUserId == m.ClerkUserId)
                    .Select(r => (RsvpResponse?)r.Response)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
