using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Persistence.Interceptors;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Outbox;

// P0-049.1 — the outbox table is persistence machinery, not a domain concept,
// so it doesn't sit in the Domain assembly and isn't picked up by the
// reflection guard in TenantScopedMarkerTests. This test pins the equivalent
// behavioural contract: OutboxMessage implements ITenantScoped, so the
// read-side query filter and the write-side stamping interceptor both cover
// it. When the dispatcher (P0-049.2) starts writing rows in the same TX as
// aggregate state, tenant isolation applies without any per-caller code.
[Collection(PostgresCollectionDefinition.Name)]
public class OutboxMessageIsolationTests
{
    private readonly PostgresFixture fixture;

    public OutboxMessageIsolationTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Insert_As_Alpha_Is_Stamped_And_Visible_To_Alpha_Only()
    {
        var eventType = $"TestEvent_{Guid.NewGuid():N}";
        var message = OutboxMessage.Create(
            id: Guid.NewGuid(),
            tenantId: PostgresFixture.AlphaTenantId,
            eventType: eventType,
            payload: "{\"kind\":\"test\"}",
            occurredAt: DateTimeOffset.UtcNow);

        await using (var alphaWrite = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            alphaWrite.OutboxMessages.Add(message);
            await alphaWrite.SaveChangesAsync();
        }

        await using var alphaRead = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        var alphaRow = await alphaRead.OutboxMessages.SingleOrDefaultAsync(m => m.EventType == eventType);
        Assert.NotNull(alphaRow);
        Assert.Equal(PostgresFixture.AlphaTenantId, alphaRow!.TenantId);

        await using var betaRead = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);
        var betaRow = await betaRead.OutboxMessages.SingleOrDefaultAsync(m => m.EventType == eventType);
        Assert.Null(betaRow);
    }

    [Fact]
    public async Task Insert_With_No_Tenant_And_No_Ambient_Is_Rejected_By_Interceptor()
    {
        // Prove the write-side interceptor covers OutboxMessage the same way it
        // covers Domain entities — no per-entity plumbing was needed, just the
        // marker.
        var message = OutboxMessage.Create(
            id: Guid.NewGuid(),
            tenantId: PostgresFixture.AlphaTenantId,
            eventType: "TestEvent",
            payload: "{}",
            occurredAt: DateTimeOffset.UtcNow);

        // Attach the message to a fresh context with an unresolved ambient
        // tenant, then intentionally clear its tenant id so the interceptor
        // sees an insert with no tenant AND no ambient to stamp from — the
        // "there is nothing to stamp it with" branch.
        await using var context = fixture.CreateContext(new TenantContext());
        context.OutboxMessages.Add(message);
        context.Entry(message).Property(nameof(OutboxMessage.TenantId)).CurrentValue = Guid.Empty;

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task IgnoreQueryFilters_Reveals_All_Tenants_Rows_For_The_Publisher_Path()
    {
        // The hosted-service publisher (P0-050) drains the outbox across all
        // tenants. Prove the same IgnoreQueryFilters escape hatch that the
        // pre-auth tenant lookup uses works for the outbox too.
        var alphaEventType = $"AlphaEvt_{Guid.NewGuid():N}";
        var betaEventType = $"BetaEvt_{Guid.NewGuid():N}";
        await InsertAsync(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug, alphaEventType);
        await InsertAsync(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug, betaEventType);

        await using var context = fixture.CreateContext(new TenantContext());
        var all = await context.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.EventType == alphaEventType || m.EventType == betaEventType)
            .ToListAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, m => m.TenantId == PostgresFixture.AlphaTenantId);
        Assert.Contains(all, m => m.TenantId == PostgresFixture.BetaTenantId);
    }

    private async Task InsertAsync(Guid tenantId, string slug, string eventType)
    {
        await using var context = fixture.CreateContext(tenantId, slug);
        context.OutboxMessages.Add(OutboxMessage.Create(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            eventType: eventType,
            payload: "{}",
            occurredAt: DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }
}
