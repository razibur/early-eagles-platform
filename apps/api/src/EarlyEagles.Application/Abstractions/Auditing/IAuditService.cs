namespace EarlyEagles.Application.Abstractions.Auditing;

/// <summary>
/// Application-layer port for the append-only audit log. The Infrastructure
/// implementation (lands with P0-047) writes to the <c>audit_events</c> table
/// in a transaction independent of the request's transaction, so a failed
/// command still leaves an audit row recording the attempt.
/// </summary>
public interface IAuditService
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken);
}
