using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Application.Payments;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Payments;

/// <summary>
/// EF <see cref="IPaymentReader"/>. Reads through the request's context, so the
/// tenant query filter scopes every result.
/// </summary>
/// <remarks>
/// Starts from payments and answers rather than members, so someone who left
/// the club still shows while their payment is open. The rows are one club's
/// session, so merging them in memory costs nothing.
/// </remarks>
public sealed class EfPaymentReader : IPaymentReader
{
    private readonly ApplicationDbContext context;

    public EfPaymentReader(ApplicationDbContext context)
    {
        this.context = context;
    }

    public async Task<IReadOnlyList<UnsettledPaymentRow>?> ListUnsettledAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var fee = await context.Sessions
            .Where(s => s.Id == sessionId)
            .Select(s => (int?)s.FeeCents)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (fee is null)
        {
            return null;
        }

        var payments = await context.SessionPayments
            .AsNoTracking()
            .Where(p => p.SessionId == sessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A free session has nothing to chase.
        var saidYes = fee == 0
            ? []
            : await context.Rsvps
                .AsNoTracking()
                .Where(r => r.SessionId == sessionId && r.Response == RsvpResponse.Yes)
                .Select(r => r.ClerkUserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var open = payments.Where(p => p.Status is not PaymentStatus.Confirmed).ToList();
        var notReported = saidYes.Except(payments.Select(p => p.ClerkUserId), StringComparer.Ordinal).ToList();

        var userIds = open.Select(p => p.ClerkUserId).Concat(notReported).ToList();
        var names = await context.TenantMembers
            .AsNoTracking()
            .Where(m => userIds.Contains(m.ClerkUserId))
            .ToDictionaryAsync(m => m.ClerkUserId, m => m.DisplayName, cancellationToken)
            .ConfigureAwait(false);

        var rows = open
            .Select(p => new UnsettledPaymentRow(p.ClerkUserId, names.GetValueOrDefault(p.ClerkUserId), p.AmountCents, p.Status, p.SenderName, p.Reference, p.RejectionReason))
            .Concat(notReported.Select(u => new UnsettledPaymentRow(u, names.GetValueOrDefault(u), fee.Value, null, null, null, null)));

        return rows
            .OrderBy(r => r.Status switch
            {
                PaymentStatus.Reported => 0,
                PaymentStatus.Rejected => 1,
                _ => 2,
            })
            .ThenBy(r => r.DisplayName ?? r.ClerkUserId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
