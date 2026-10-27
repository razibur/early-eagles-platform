namespace EarlyEagles.Application.Auditing;

/// <summary>One audit-log row as the audit query returns it (P0-048).</summary>
public sealed record AuditEventSummary(
    Guid Id,
    string Action,
    bool Success,
    string? FailureReason,
    string? ActorClerkUserId,
    DateTimeOffset OccurredAt);
