using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Integration.Outbox;

// P0-050.1 — what the poll actually selects, against real Postgres. The loop
// mechanics are unit-tested; this is the query: pending only, oldest first,
// capped at the batch size, and — the one that would be a data leak anywhere
// else — every tenant at once.
//
// Each test first stamps the outbox clean, because the fixture database is
// shared across the whole integration collection and other tests leave pending
// rows behind. Anything asserting on "what is pending" has to do the same.
[Collection(PostgresCollectionDefinition.Name)]
public class OutboxProcessorPollTests
{
    private readonly PostgresFixture fixture;

    public OutboxProcessorPollTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Poll_Returns_Pending_Rows_From_Every_Tenant()
    {
        // One process delivers for all tenants, so the drain is the one read
        // that legitimately crosses the boundary. Its context has no ambient
        // tenant, so without IgnoreQueryFilters this returns nothing at all —
        // the outbox would silently never drain.
        await MarkEverythingProcessedAsync();

        var alpha = NewMessage(PostgresFixture.AlphaTenantId, DateTimeOffset.UtcNow.AddMinutes(-2));
        var beta = NewMessage(PostgresFixture.BetaTenantId, DateTimeOffset.UtcNow.AddMinutes(-1));
        await InsertAsync(alpha, beta);

        await using var provider = NewProvider();
        var pending = await NewProcessor(provider).PollOnceAsync(CancellationToken.None);

        Assert.Contains(pending, message => message.Id == alpha.Id);
        Assert.Contains(pending, message => message.Id == beta.Id);
    }

    [Fact]
    public async Task Poll_Skips_Rows_That_Are_Already_Processed()
    {
        await MarkEverythingProcessedAsync();

        var delivered = NewMessage(PostgresFixture.AlphaTenantId, DateTimeOffset.UtcNow.AddMinutes(-5));
        var queued = NewMessage(PostgresFixture.AlphaTenantId, DateTimeOffset.UtcNow.AddMinutes(-4));
        await InsertAsync(delivered, queued);
        await MarkProcessedAsync(delivered.Id);

        await using var provider = NewProvider();
        var pending = await NewProcessor(provider).PollOnceAsync(CancellationToken.None);

        Assert.DoesNotContain(pending, message => message.Id == delivered.Id);
        Assert.Contains(pending, message => message.Id == queued.Id);
    }

    [Fact]
    public async Task Poll_Returns_The_Oldest_Rows_First()
    {
        // Events describe a sequence of things that happened; delivering them
        // newest-first would hand subscribers the story backwards.
        await MarkEverythingProcessedAsync();

        var now = DateTimeOffset.UtcNow;
        var youngest = NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-1));
        var middle = NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-2));
        var oldest = NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-3));
        await InsertAsync(youngest, middle, oldest);

        await using var provider = NewProvider();
        var pending = await NewProcessor(provider).PollOnceAsync(CancellationToken.None);

        Assert.Equal(
            [oldest.Id, middle.Id, youngest.Id],
            pending.Select(message => message.Id));
    }

    [Fact]
    public async Task Poll_Never_Returns_More_Than_The_Batch_Size()
    {
        // A backlog must not turn one tick into an unbounded read.
        await MarkEverythingProcessedAsync();

        var now = DateTimeOffset.UtcNow;
        var messages = Enumerable.Range(1, 5)
            .Select(minutes => NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-minutes)))
            .ToArray();
        await InsertAsync(messages);

        await using var provider = NewProvider();
        var pending = await NewProcessor(provider, batchSize: 2).PollOnceAsync(CancellationToken.None);

        Assert.Equal(2, pending.Count);
    }

    private static OutboxMessage NewMessage(Guid tenantId, DateTimeOffset occurredAt) =>
        OutboxMessage.Create(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            eventType: $"PollEvt_{Guid.NewGuid():N}",
            payload: "{}",
            occurredAt: occurredAt.ToUniversalTime());

    private static OutboxProcessor NewProcessor(IServiceProvider provider, int batchSize = 100) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions { BatchSize = batchSize }),
            NullLogger<OutboxProcessor>.Instance);

    // Mirrors how the hosted service resolves its context: a scope with no
    // ambient tenant, exactly as a background thread has.
    private ServiceProvider NewProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => fixture.CreateContext(new TenantContext()));
        return services.BuildServiceProvider();
    }

    private async Task InsertAsync(params OutboxMessage[] messages)
    {
        await using var context = fixture.CreateContext(new TenantContext());
        context.OutboxMessages.AddRange(messages);
        await context.SaveChangesAsync();
    }

    private async Task MarkProcessedAsync(Guid id)
    {
        // Raw SQL because stamping ProcessedAt is P0-050.2's job — the aggregate
        // has no public way to do it yet, and inventing one here would
        // pre-empt that task.
        await using var context = fixture.CreateContext(new TenantContext());
        await context.Database.ExecuteSqlAsync(
            $"UPDATE outbox_messages SET processed_at = now() WHERE id = {id}");
    }

    private async Task MarkEverythingProcessedAsync()
    {
        await using var context = fixture.CreateContext(new TenantContext());
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE outbox_messages SET processed_at = now() WHERE processed_at IS NULL");
    }
}
