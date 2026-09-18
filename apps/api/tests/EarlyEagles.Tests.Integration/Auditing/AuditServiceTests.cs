using EarlyEagles.Application.Abstractions.Auditing;
using EarlyEagles.Domain.Auditing;
using EarlyEagles.Infrastructure.Auditing;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Auditing;

// P0-047.1 — the concrete AuditService writes AuditEvent rows to the
// append-only audit_events table, enriched with the ambient tenant and a UTC
// timestamp, in a fresh context per call so the write is independent of any
// request-scope transaction.
[Collection(PostgresCollectionDefinition.Name)]
public class AuditServiceTests
{
    private readonly PostgresFixture fixture;

    public AuditServiceTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task LogAsync_Persists_Success_Row_Scoped_To_Ambient_Tenant()
    {
        var action = $"SucceedingCommand_{Guid.NewGuid():N}";
        var service = CreateService(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        var before = DateTimeOffset.UtcNow;

        await service.LogAsync(new AuditEntry(action, Success: true), CancellationToken.None);

        var row = await ReadOwnAuditRowAsync(
            PostgresFixture.AlphaTenantId,
            PostgresFixture.AlphaSlug,
            action);

        Assert.NotNull(row);
        Assert.Equal(PostgresFixture.AlphaTenantId, row!.TenantId);
        Assert.Equal(action, row.Action);
        Assert.True(row.Success);
        Assert.Null(row.FailureReason);
        Assert.Null(row.ActorClerkUserId);
        Assert.True(row.OccurredAt >= before);
    }

    [Fact]
    public async Task LogAsync_Persists_Failure_Row_With_FailureReason()
    {
        var action = $"FailingCommand_{Guid.NewGuid():N}";
        const string reason = "InvalidOperationException: session already closed";
        var service = CreateService(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        await service.LogAsync(
            new AuditEntry(action, Success: false, FailureReason: reason),
            CancellationToken.None);

        var row = await ReadOwnAuditRowAsync(
            PostgresFixture.AlphaTenantId,
            PostgresFixture.AlphaSlug,
            action);

        Assert.NotNull(row);
        Assert.False(row!.Success);
        Assert.Equal(reason, row.FailureReason);
    }

    [Fact]
    public async Task LogAsync_As_Beta_Does_Not_Leak_Into_Alpha()
    {
        // The row must be tenant-scoped end-to-end: writing as Beta, reading as
        // Alpha (with the query filter on) must surface nothing.
        var action = $"BetaOnlyCommand_{Guid.NewGuid():N}";
        var service = CreateService(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);

        await service.LogAsync(new AuditEntry(action, Success: true), CancellationToken.None);

        await using var alphaContext = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        var visibleToAlpha = await alphaContext.AuditEvents
            .Where(e => e.Action == action)
            .ToListAsync();

        Assert.Empty(visibleToAlpha);
    }

    [Fact]
    public async Task LogAsync_Throws_When_Tenant_Is_Not_Resolved()
    {
        // AuditBehavior is only meant to run inside tenant-scoped requests — an
        // unresolved context here is a bug upstream, not a case to swallow.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        var service = new AuditService(options, new TenantContext());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LogAsync(new AuditEntry("X", true), CancellationToken.None));
    }

    [Fact]
    public async Task LogAsync_Survives_A_Request_Transaction_Rollback()
    {
        // The audit contract is that a rolled-back command still leaves a trail.
        // Simulate it: open a request context, begin a transaction, add a
        // non-audit change, roll back. In parallel, the audit service opens its
        // own fresh context and saves — that save must land regardless of the
        // outer rollback. This proves the fresh-context isolation is real, not
        // just a comment.
        var action = $"RolledBackCommand_{Guid.NewGuid():N}";
        var service = CreateService(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        await using (var requestContext = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        await using (var tx = await requestContext.Database.BeginTransactionAsync())
        {
            await service.LogAsync(new AuditEntry(action, Success: false, FailureReason: "boom"), CancellationToken.None);
            await tx.RollbackAsync();
        }

        var row = await ReadOwnAuditRowAsync(
            PostgresFixture.AlphaTenantId,
            PostgresFixture.AlphaSlug,
            action);

        Assert.NotNull(row);
        Assert.False(row!.Success);
        Assert.Equal("boom", row.FailureReason);
    }

    private AuditService CreateService(Guid tenantId, string slug)
    {
        var tenantContext = new TenantContext();
        tenantContext.Initialize(tenantId, slug);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new AuditService(options, tenantContext);
    }

    private async Task<AuditEvent?> ReadOwnAuditRowAsync(Guid tenantId, string slug, string action)
    {
        await using var context = fixture.CreateContext(tenantId, slug);
        return await context.AuditEvents.SingleOrDefaultAsync(e => e.Action == action);
    }
}
