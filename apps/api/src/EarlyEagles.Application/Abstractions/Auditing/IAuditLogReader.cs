using EarlyEagles.Application.Auditing;

namespace EarlyEagles.Application.Abstractions.Auditing;

/// <summary>
/// Read side of the audit log (P0-048). Implementations must read through the
/// tenant query filter, so every result belongs to the ambient tenant.
/// </summary>
public interface IAuditLogReader
{
    /// <summary>
    /// Up to <paramref name="take"/> events strictly older than
    /// <paramref name="after"/> (or the newest, when it is <c>null</c>), ordered
    /// by <c>OccurredAt</c> then <c>Id</c>, both descending.
    /// </summary>
    Task<IReadOnlyList<AuditEventSummary>> ListAsync(AuditEventCursor? after, int take, CancellationToken cancellationToken);
}
