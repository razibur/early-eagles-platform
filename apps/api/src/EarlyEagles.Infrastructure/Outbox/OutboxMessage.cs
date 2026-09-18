using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// One pending domain event queued for publication. The outbox pattern lets a
/// command persist its state change and its outgoing events in the same
/// database transaction — either both land or neither does — while a
/// background hosted service (P0-050) later drains the queue to the message
/// bus. This decouples "the domain event was raised" from "the message reached
/// the bus" so a bus outage doesn't roll back a legitimate command.
/// </summary>
/// <remarks>
/// <para>
/// Lives in Infrastructure rather than Domain because it is persistence
/// machinery, not a business concept — but it implements
/// <see cref="ITenantScoped"/> so the read-side query filter (P0-032.2) and
/// the write-side tenant-stamping interceptor (P0-034.4) apply automatically.
/// The hosted-service publisher (P0-050) reads across all tenants with
/// <c>IgnoreQueryFilters</c> — the same platform-work opt-out
/// [<c>docs/runbooks/tenant-isolation-tests.md §7</c>] used elsewhere.
/// </para>
/// <para>
/// Event payloads are serialised as JSON strings. Storing the CLR-side event
/// type as <see cref="EventType"/> (the full name, no assembly info) is enough
/// for the publisher to route without pinning to an assembly version — the
/// subscriber side reads the topic name / payload shape, not the sender's
/// assembly.
/// </para>
/// </remarks>
public sealed class OutboxMessage : ITenantScoped
{
    public const int EventTypeMaxLength = 256;

    // EF binds this constructor on materialisation — its parameter names match
    // the mapped columns. Domain callers reach it through Create, which
    // validates first.
    private OutboxMessage(
        Guid id,
        Guid tenantId,
        string eventType,
        string payload,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    // Parameterless ctor for EF Core materialisation fallback; strings default
    // to empty so the mapped state is well-formed before EF sets real values.
    private OutboxMessage()
    {
        EventType = string.Empty;
        Payload = string.Empty;
    }

    public Guid Id { get; }

    /// <summary>
    /// Owning tenant. Satisfies <see cref="ITenantScoped"/> — the global query
    /// filter scopes reads by this column, and the write-side interceptor stamps
    /// or validates it.
    /// </summary>
    public Guid TenantId { get; }

    /// <summary>
    /// CLR full name of the domain event, e.g.
    /// <c>EarlyEagles.Domain.Tenants.TenantCreated</c>. The publisher uses this
    /// to route to a bus topic or subscription; version-tolerant (no assembly
    /// info).
    /// </summary>
    public string EventType { get; }

    /// <summary>JSON-serialised event body.</summary>
    public string Payload { get; }

    /// <summary>When the domain event was raised on the aggregate (UTC).</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// When the publisher successfully handed the message to the bus.
    /// <c>null</c> = still pending; the poller filters on this column, which is
    /// backed by a partial index for cheap "unprocessed only" scans.
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>
    /// Number of publisher attempts so far. Bumped on every attempt regardless
    /// of outcome, so a poison message that always fails is visible and
    /// bookendable.
    /// </summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// Last publisher failure text; <c>null</c> if the last attempt succeeded
    /// or none has run yet. Not the full stack trace — traces live in the log
    /// pipeline; the outbox records the retry state.
    /// </summary>
    public string? LastError { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        Guid tenantId,
        string eventType,
        string payload,
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

        ValidateEventType(eventType);
        ValidatePayload(payload);

        if (occurredAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("OccurredAt must be a UTC timestamp.", nameof(occurredAt));
        }

        return new OutboxMessage(id, tenantId, eventType, payload, occurredAt);
    }

    private static void ValidateEventType(string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type must not be empty or whitespace.", nameof(eventType));
        }

        if (eventType.Length > EventTypeMaxLength)
        {
            throw new ArgumentException(
                $"Event type must be at most {EventTypeMaxLength} characters; got {eventType.Length}.",
                nameof(eventType));
        }
    }

    private static void ValidatePayload(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload must not be empty or whitespace.", nameof(payload));
        }
    }
}
