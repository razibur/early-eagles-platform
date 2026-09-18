using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Auditing;

/// <summary>
/// One row in the append-only audit log. Records the fact that a state-changing
/// operation was attempted against the platform: what it was, who ran it, when
/// it ran, and whether it succeeded — with the failure reason attached when it
/// did not. The audit log is the durable record of intent independent of the
/// outcome, so both successful commands and rolled-back ones leave a trail.
/// </summary>
/// <remarks>
/// <para>
/// Every audit event is tenant-scoped (<see cref="ITenantScoped"/>) — the log
/// itself is per-tenant, and the same global query filter that isolates every
/// other entity keeps one tenant from reading another's history. Rows are
/// append-only: there is no <c>Update</c> or <c>Delete</c> API on the type, and
/// a Postgres trigger (P0-046.2) enforces the same at the storage layer as a
/// second line of defence.
/// </para>
/// <para>
/// Ambient enrichment (tenant, actor, timestamp) is the audit-service
/// implementation's job (P0-047); the domain type just requires the values to
/// be supplied and validates them. That keeps the aggregate testable without
/// touching <c>ITenantContext</c> or the clock, and keeps ambient reads out of
/// the domain layer.
/// </para>
/// </remarks>
public sealed class AuditEvent : AggregateRoot<Guid>, ITenantScoped
{
    public const int ActionMaxLength = 256;
    public const int ActorClerkUserIdMaxLength = 256;
    public const int FailureReasonMaxLength = 2000;

    private AuditEvent(
        Guid id,
        Guid tenantId,
        string action,
        bool success,
        string? failureReason,
        string? actorClerkUserId,
        DateTimeOffset occurredAt)
        : base(id)
    {
        TenantId = tenantId;
        Action = action;
        Success = success;
        FailureReason = failureReason;
        ActorClerkUserId = actorClerkUserId;
        OccurredAt = occurredAt;
    }

    // Parameterless ctor for EF Core materialisation. Strings default to empty
    // so the mapped state is well-formed before EF sets the real values.
    private AuditEvent()
    {
        Action = string.Empty;
    }

    /// <summary>
    /// Owning tenant. Satisfies <see cref="ITenantScoped"/> — the global query
    /// filter scopes reads by this column, and the write-side interceptor stamps
    /// or validates it (P0-032.2, P0-034.4).
    /// </summary>
    public Guid TenantId { get; }

    /// <summary>
    /// The user-intent action. Conventionally the command's type name, e.g.
    /// <c>RecordAttendanceCommand</c>, so the log reads as the sequence of
    /// verbs the platform was asked to perform.
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// <c>true</c> when the handler completed; <c>false</c> when it threw.
    /// A failed audit event carries <see cref="FailureReason"/>.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Exception type + message when <see cref="Success"/> is <c>false</c>;
    /// <c>null</c> on success. Deliberately not the full stack trace — stack
    /// traces live in the log/tracing pipeline; the audit log records intent.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Clerk user id of the caller, when the action came from an authenticated
    /// request. <c>null</c> for system-originated actions (webhooks, background
    /// jobs, migrations) where no user is on the wire.
    /// </summary>
    public string? ActorClerkUserId { get; }

    /// <summary>
    /// When the action was recorded, as a UTC instant. The log is a single
    /// chronology across the tenant, not localised — display-layer code
    /// projects into the reader's zone.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }

    public static AuditEvent Create(
        Guid id,
        Guid tenantId,
        string action,
        bool success,
        string? failureReason,
        string? actorClerkUserId,
        DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        ValidateAction(action);
        ValidateFailure(success, failureReason);
        ValidateActor(actorClerkUserId);

        // A UTC-normalised chronology is easier to reason about than mixed
        // offsets, and prevents "which zone?" ambiguity in incident review.
        if (occurredAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("OccurredAt must be a UTC timestamp.", nameof(occurredAt));
        }

        return new AuditEvent(id, tenantId, action, success, failureReason, actorClerkUserId, occurredAt);
    }

    private static void ValidateAction(string action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action must not be empty or whitespace.", nameof(action));
        }

        if (action.Length > ActionMaxLength)
        {
            throw new ArgumentException(
                $"Action must be at most {ActionMaxLength} characters; got {action.Length}.",
                nameof(action));
        }
    }

    private static void ValidateFailure(bool success, string? failureReason)
    {
        if (success)
        {
            if (failureReason is not null)
            {
                throw new ArgumentException(
                    "Successful audit events must not carry a failure reason.",
                    nameof(failureReason));
            }

            return;
        }

        if (failureReason is null)
        {
            throw new ArgumentException(
                "Failed audit events must carry a failure reason.",
                nameof(failureReason));
        }

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException(
                "Failure reason must not be empty or whitespace.",
                nameof(failureReason));
        }

        if (failureReason.Length > FailureReasonMaxLength)
        {
            throw new ArgumentException(
                $"Failure reason must be at most {FailureReasonMaxLength} characters; got {failureReason.Length}.",
                nameof(failureReason));
        }
    }

    private static void ValidateActor(string? actorClerkUserId)
    {
        // Null is fine — system-originated actions have no user attached.
        if (actorClerkUserId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(actorClerkUserId))
        {
            throw new ArgumentException(
                "Actor clerk user id must not be empty or whitespace when provided.",
                nameof(actorClerkUserId));
        }

        if (actorClerkUserId.Length > ActorClerkUserIdMaxLength)
        {
            throw new ArgumentException(
                $"Actor clerk user id must be at most {ActorClerkUserIdMaxLength} characters; got {actorClerkUserId.Length}.",
                nameof(actorClerkUserId));
        }
    }
}
