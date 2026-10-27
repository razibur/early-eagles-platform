using Mediator;

namespace EarlyEagles.Application.Auditing;

/// <summary>
/// Lists the ambient tenant's audit log, newest first, one page at a time
/// (P0-048). A query, so <c>AuditBehavior</c> and <c>TransactionBehavior</c>
/// pass it through: reading the log doesn't add to it.
/// </summary>
/// <param name="Limit">Page size, 1–<see cref="MaxLimit"/>.</param>
/// <param name="Cursor">The previous page's <see cref="AuditEventPage.NextCursor"/>, or <c>null</c> for the first page.</param>
public sealed record ListAuditEventsQuery(int Limit, string? Cursor) : IQuery<AuditEventPage>
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;
}
