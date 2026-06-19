namespace EarlyEagles.Application.Abstractions.Auditing;

/// <summary>
/// One row in the append-only audit log. Captures the user-intent action and its
/// outcome; the audit-service implementation enriches with tenant, actor, and
/// timestamp from the ambient context (P0-047).
/// </summary>
/// <param name="Action">
///   Conventionally the command's type name, e.g. <c>RecordAttendanceCommand</c>.
/// </param>
/// <param name="Success">
///   <c>true</c> if the handler completed; <c>false</c> if it threw.
/// </param>
/// <param name="FailureReason">
///   Exception type + message when <paramref name="Success"/> is <c>false</c>;
///   <c>null</c> on success.
/// </param>
public sealed record AuditEntry(
    string Action,
    bool Success,
    string? FailureReason = null);
