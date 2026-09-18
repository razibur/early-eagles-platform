using EarlyEagles.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-046.2 — the storage-layer backstop for the AuditEvent append-only
// contract. The domain type has no updater methods, but ExecuteUpdate /
// ExecuteDelete bypass the change tracker (and thus the write-side interceptor),
// and hand-rolled SQL bypasses both. The trigger the migration installs raises
// a SQLSTATE P0001 exception on any UPDATE, DELETE, or TRUNCATE against
// audit_events, and Npgsql surfaces that as a PostgresException. This test
// pins that behaviour end-to-end against real Postgres.
[Collection(PostgresCollectionDefinition.Name)]
public class AuditEventsAppendOnlyTests
{
    private readonly PostgresFixture fixture;

    public AuditEventsAppendOnlyTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Insert_As_Alpha_Succeeds()
    {
        // Sanity: inserts are the whole point of the log — they must work.
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var evt = NewAuditEvent(PostgresFixture.AlphaTenantId);
        context.AuditEvents.Add(evt);
        await context.SaveChangesAsync();

        Assert.NotNull(await context.AuditEvents.FirstOrDefaultAsync(e => e.Id == evt.Id));
    }

    [Fact]
    public async Task ExecuteUpdate_On_Own_Row_Is_Rejected_By_The_Trigger()
    {
        // ExecuteUpdate emits a raw SQL UPDATE — no change tracker, no
        // SaveChanges, no interceptor. The trigger is what stops it.
        var evtId = await SeedOneEventAsync(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await context.AuditEvents
                .Where(e => e.Id == evtId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Action, "TamperedAction"));
        });

        Assert.Contains("append-only", exception.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteDelete_On_Own_Row_Is_Rejected_By_The_Trigger()
    {
        var evtId = await SeedOneEventAsync(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await context.AuditEvents.Where(e => e.Id == evtId).ExecuteDeleteAsync();
        });

        Assert.Contains("append-only", exception.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Truncate_Is_Rejected_By_The_Trigger()
    {
        // TRUNCATE doesn't fire per-row triggers, so a BEFORE UPDATE OR DELETE
        // FOR EACH ROW trigger alone would leave a hole. The migration installs
        // a second BEFORE TRUNCATE FOR EACH STATEMENT trigger to close it.
        await using var context = fixture.CreateContext(new EarlyEagles.Infrastructure.Tenancy.TenantContext());

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE audit_events;");
        });

        Assert.Contains("append-only", exception.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    private static AuditEvent NewAuditEvent(Guid tenantId) =>
        AuditEvent.Create(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            action: "RecordAttendanceCommand",
            success: true,
            failureReason: null,
            actorClerkUserId: "user_test",
            occurredAt: DateTimeOffset.UtcNow);

    private async Task<Guid> SeedOneEventAsync(Guid tenantId, string slug)
    {
        await using var context = fixture.CreateContext(tenantId, slug);

        var evt = NewAuditEvent(tenantId);
        context.AuditEvents.Add(evt);
        await context.SaveChangesAsync();
        return evt.Id;
    }
}
