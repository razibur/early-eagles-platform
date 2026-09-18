using EarlyEagles.Domain.Auditing;
using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Tests.Unit.Auditing;

// P0-046.1 — invariants for the AuditEvent aggregate. The type's contract is
// asymmetric: Success requires FailureReason == null, and !Success requires
// FailureReason to be present and non-empty. Actor is optional (system actions
// have no user). Timestamps must be UTC — the audit log is one chronology.
public class AuditEventTests
{
    private const string ValidAction = "RecordAttendanceCommand";
    private const string ValidActor = "user_2abcXYZ";
    private const string ValidFailure = "InvalidOperationException: session is closed";
    private static readonly Guid ValidId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ValidTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset ValidTimestamp = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Success_Sets_All_Properties()
    {
        var evt = CreateValid();

        Assert.Equal(ValidId, evt.Id);
        Assert.Equal(ValidTenantId, evt.TenantId);
        Assert.Equal(ValidAction, evt.Action);
        Assert.True(evt.Success);
        Assert.Null(evt.FailureReason);
        Assert.Equal(ValidActor, evt.ActorClerkUserId);
        Assert.Equal(ValidTimestamp, evt.OccurredAt);
    }

    [Fact]
    public void Create_Failure_Sets_FailureReason()
    {
        var evt = CreateValid(success: false, failureReason: ValidFailure);

        Assert.False(evt.Success);
        Assert.Equal(ValidFailure, evt.FailureReason);
    }

    [Fact]
    public void Create_With_Null_Actor_Is_Allowed()
    {
        // System-originated actions (webhooks, background jobs, migrations) run
        // without a user on the wire — the null must round-trip.
        var evt = CreateValid(actor: null);

        Assert.Null(evt.ActorClerkUserId);
    }

    [Fact]
    public void Empty_Id_Throws()
    {
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(
            id: Guid.Empty,
            tenantId: ValidTenantId,
            action: ValidAction,
            success: true,
            failureReason: null,
            actorClerkUserId: ValidActor,
            occurredAt: ValidTimestamp));
    }

    [Fact]
    public void Empty_TenantId_Throws()
    {
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(
            id: ValidId,
            tenantId: Guid.Empty,
            action: ValidAction,
            success: true,
            failureReason: null,
            actorClerkUserId: ValidActor,
            occurredAt: ValidTimestamp));
    }

    [Fact]
    public void Null_Action_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AuditEvent.Create(
            id: ValidId,
            tenantId: ValidTenantId,
            action: null!,
            success: true,
            failureReason: null,
            actorClerkUserId: ValidActor,
            occurredAt: ValidTimestamp));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Empty_Or_Whitespace_Action_Throws(string action)
    {
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(
            id: ValidId,
            tenantId: ValidTenantId,
            action: action,
            success: true,
            failureReason: null,
            actorClerkUserId: ValidActor,
            occurredAt: ValidTimestamp));
    }

    [Fact]
    public void Overlong_Action_Throws()
    {
        var action = new string('x', AuditEvent.ActionMaxLength + 1);

        Assert.Throws<ArgumentException>(() => AuditEvent.Create(
            id: ValidId,
            tenantId: ValidTenantId,
            action: action,
            success: true,
            failureReason: null,
            actorClerkUserId: ValidActor,
            occurredAt: ValidTimestamp));
    }

    [Fact]
    public void Success_With_FailureReason_Throws()
    {
        // The invariant reads both ways — a success carrying a failure reason
        // implies the log lied about the outcome, so reject at construction.
        Assert.Throws<ArgumentException>(() => CreateValid(success: true, failureReason: ValidFailure));
    }

    [Fact]
    public void Failure_Without_FailureReason_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateValid(success: false, failureReason: null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Failure_With_Empty_Or_Whitespace_FailureReason_Throws(string failure)
    {
        Assert.Throws<ArgumentException>(() => CreateValid(success: false, failureReason: failure));
    }

    [Fact]
    public void Failure_With_Overlong_FailureReason_Throws()
    {
        var failure = new string('x', AuditEvent.FailureReasonMaxLength + 1);

        Assert.Throws<ArgumentException>(() => CreateValid(success: false, failureReason: failure));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Actor_Empty_Or_Whitespace_Throws(string actor)
    {
        // Null is fine (system origin); empty string is malformed input, not
        // "no user" — distinguish the two.
        Assert.Throws<ArgumentException>(() => CreateValid(actor: actor));
    }

    [Fact]
    public void Overlong_Actor_Throws()
    {
        var actor = new string('x', AuditEvent.ActorClerkUserIdMaxLength + 1);

        Assert.Throws<ArgumentException>(() => CreateValid(actor: actor));
    }

    [Fact]
    public void Non_Utc_Timestamp_Throws()
    {
        var pacific = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.FromHours(-8));

        Assert.Throws<ArgumentException>(() => AuditEvent.Create(
            id: ValidId,
            tenantId: ValidTenantId,
            action: ValidAction,
            success: true,
            failureReason: null,
            actorClerkUserId: ValidActor,
            occurredAt: pacific));
    }

    [Fact]
    public void AuditEvent_Is_TenantScoped_And_Reports_The_Right_Tenant()
    {
        // The reflection guard in TenantScopedMarkerTests already asserts every
        // concrete Entity<> implements the marker; this pins the behavioural
        // contract: TenantId is the ambient tenant, not the aggregate id.
        var evt = CreateValid();

        Assert.IsAssignableFrom<ITenantScoped>(evt);
        Assert.Equal(ValidTenantId, ((ITenantScoped)evt).TenantId);
        Assert.NotEqual(evt.Id, evt.TenantId);
    }

    private static AuditEvent CreateValid(
        bool success = true,
        string? failureReason = null,
        string? actor = ValidActor)
    {
        return AuditEvent.Create(
            id: ValidId,
            tenantId: ValidTenantId,
            action: ValidAction,
            success: success,
            failureReason: failureReason,
            actorClerkUserId: actor,
            occurredAt: ValidTimestamp);
    }
}
