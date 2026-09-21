using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Persistence.Interceptors;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EarlyEagles.Tests.Integration.Outbox;

// P0-049.2 — the claim under test is atomicity: an event staged by the
// dispatcher reaches Postgres through the same transaction as the state change
// that raised it. The unit tests pin the mapping rules; these prove the row and
// the state really do travel together against a real database.
[Collection(PostgresCollectionDefinition.Name)]
public class DomainEventOutboxTests
{
    private readonly PostgresFixture fixture;

    public DomainEventOutboxTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Every_Context_Drains_Domain_Events_On_Save()
    {
        // Registered in OnConfiguring rather than at a call site, so no way of
        // building a context can skip the drain. If this ever regresses, domain
        // events would vanish silently instead of failing a test.
        using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var registered = context.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()!
            .Interceptors!;

        Assert.Contains(registered, interceptor => interceptor is DomainEventOutboxInterceptor);
    }

    [Fact]
    public async Task Dispatched_Events_Commit_With_The_State_Change()
    {
        // Provisioning shape: a platform-level context with no ambient tenant
        // creates the tenant and queues the event announcing it, in one
        // transaction. Both carry an explicit tenant id, which is the write-side
        // escape hatch the stamping interceptor allows for platform work.
        var tenantId = Guid.NewGuid();
        var slug = $"outbox-{Guid.NewGuid():N}";
        var raised = new SampleEvent(slug);

        await using (var context = fixture.CreateContext(new TenantContext()))
        {
            var unitOfWork = new EfUnitOfWork(context);
            await using var transaction = await unitOfWork.BeginTransactionAsync(CancellationToken.None);

            context.Tenants.Add(Tenant.Create(tenantId, slug, "Outbox Club"));
            new OutboxDomainEventDispatcher(context).Dispatch(tenantId, [raised]);

            await context.SaveChangesAsync();
            await transaction.CommitAsync(CancellationToken.None);
        }

        // Read back as the new tenant, on a fresh connection, so only committed
        // rows are visible and the query filter has to let both through.
        await using var reader = fixture.CreateContext(tenantId, slug);

        Assert.True(await reader.Tenants.AnyAsync(t => t.Id == tenantId));

        var message = await reader.OutboxMessages.SingleAsync(m => m.Id == raised.Id);
        Assert.Equal(tenantId, message.TenantId);
        Assert.Equal(typeof(SampleEvent).FullName, message.EventType);
        Assert.Contains(slug, message.Payload, StringComparison.Ordinal);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task A_Dispatched_Event_Belongs_To_The_Dispatching_Tenant_Only()
    {
        // Outbox rows are tenant data like any other: Beta must not see Alpha's
        // queued events, even though the publisher will later read across all of
        // them with IgnoreQueryFilters.
        var raised = new SampleEvent("alpha-only");

        await using (var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            new OutboxDomainEventDispatcher(context).Dispatch(PostgresFixture.AlphaTenantId, [raised]);
            await context.SaveChangesAsync();
        }

        await using var beta = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);
        Assert.False(await beta.OutboxMessages.AnyAsync(m => m.Id == raised.Id));

        await using var platform = fixture.CreateContext(new TenantContext());
        Assert.True(await platform.OutboxMessages.IgnoreQueryFilters().AnyAsync(m => m.Id == raised.Id));
    }

    private sealed record SampleEvent(string Detail) : IDomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();

        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
