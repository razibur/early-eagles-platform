namespace EarlyEagles.Application.Auditing;

/// <summary>
/// One page of the audit log, newest first. <see cref="NextCursor"/> is the
/// token for the following (older) page, or <c>null</c> on the last page.
/// </summary>
public sealed record AuditEventPage(IReadOnlyList<AuditEventSummary> Items, string? NextCursor);
