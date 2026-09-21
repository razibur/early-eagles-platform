using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Integration.Outbox;

// P0-050.1 + P0-050.2 — one drain against real Postgres: what the read selects
// (pending only, oldest first, capped, every tenant at once) and what the
// outcome does to the row (published → stamped and gone from the queue; failed
// → still pending with its attempt counted).
//
// Each test first stamps the outbox clean, because the fixture database is
// shared across the whole integration collection and other tests leave pending
// rows behind. Anything asserting on "what is pending" has to do the same.
[Collection(PostgresCollectionDefinition.Name)]
public class OutboxProcessorDrainTests
{
    private readonly PostgresFixture fixture;

    public OutboxProcessorDrainTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Drain_Publishes_Rows_From_Every_Tenant()
    {
        // One process delivers for all tenants, so the drain is the one read
        // that legitimately crosses the boundary. Its context has no ambient
        // tenant, so without IgnoreQueryFilters this returns nothing at all —
        // the outbox would silently never drain.
        await MarkEverythingProcessedAsync();

        var alpha = NewMessage(PostgresFixture.AlphaTenantId, DateTimeOffset.UtcNow.AddMinutes(-2));
        var beta = NewMessage(PostgresFixture.BetaTenantId, DateTimeOffset.UtcNow.AddMinutes(-1));
        await InsertAsync(alpha, beta);

        var publisher = new RecordingPublisher();
        var published = await DrainAsync(publisher);

        Assert.Equal(2, published);
        Assert.Contains(publisher.Published, message => message.Id == alpha.Id);
        Assert.Contains(publisher.Published, message => message.Id == beta.Id);
    }

    [Fact]
    public async Task Drain_Skips_Rows_That_Are_Already_Processed()
    {
        await MarkEverythingProcessedAsync();

        var delivered = NewMessage(PostgresFixture.AlphaTenantId, DateTimeOffset.UtcNow.AddMinutes(-5));
        var queued = NewMessage(PostgresFixture.AlphaTenantId, DateTimeOffset.UtcNow.AddMinutes(-4));
        await InsertAsync(delivered, queued);
        await MarkProcessedAsync(delivered.Id);

        var publisher = new RecordingPublisher();
        await DrainAsync(publisher);

        Assert.DoesNotContain(publisher.Published, message => message.Id == delivered.Id);
        Assert.Contains(publisher.Published, message => message.Id == queued.Id);
    }

    [Fact]
    public async Task Drain_Publishes_The_Oldest_Rows_First()
    {
        // Events describe a sequence of things that happened; delivering them
        // newest-first would hand subscribers the story backwards.
        await MarkEverythingProcessedAsync();

        var now = DateTimeOffset.UtcNow;
        var youngest = NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-1));
        var middle = NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-2));
        var oldest = NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-3));
        await InsertAsync(youngest, middle, oldest);

        var publisher = new RecordingPublisher();
        await DrainAsync(publisher);

        Assert.Equal(
            [oldest.Id, middle.Id, youngest.Id],
            publisher.Published.Select(message => message.Id));
    }

    [Fact]
    public async Task Drain_Never_Takes_More_Than_The_Batch_Size()
    {
        // A backlog must not turn one tick into an unbounded read.
        await MarkEverythingProcessedAsync();

        var now = DateTimeOffset.UtcNow;
        var messages = Enumerable.Range(1, 5)
            .Select(minutes => NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-minutes)))
            .ToArray();
        await InsertAsync(messages);

        var publisher = new RecordingPublisher();
        var published = await DrainAsync(publisher, batchSize: 2);

        Assert.Equal(2, published);
    }

    [Fact]
    public async Task A_Published_Message_Is_Stamped_And_Leaves_The_Queue()
    {
        await MarkEverythingProcessedAsync();

        var message = NewMessage(PostgresFixture.AlphaTenantId, DateTimeOffset.UtcNow.AddMinutes(-1));
        await InsertAsync(message);

        await DrainAsync(new RecordingPublisher());

        var stored = await ReadAsync(message.Id);
        Assert.NotNull(stored.ProcessedAt);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Null(stored.LastError);

        // The stamp is what keeps the next tick from sending it again — the row
        // drops out of the pending partial index entirely.
        var second = new RecordingPublisher();
        await DrainAsync(second);
        Assert.DoesNotContain(second.Published, published => published.Id == message.Id);
    }

    [Fact]
    public async Task A_Failed_Publish_Stays_Pending_And_Records_Why()
    {
        // A bus outage must not lose the event: the row keeps its place in the
        // queue, and the attempt count is what makes a message that never
        // succeeds visible instead of invisibly looping.
        await MarkEverythingProcessedAsync();

        var message = NewMessage(PostgresFixture.AlphaTenantId, DateTimeOffset.UtcNow.AddMinutes(-1));
        await InsertAsync(message);

        var published = await DrainAsync(new RecordingPublisher(_ => true));
        Assert.Equal(0, published);

        var afterFirst = await ReadAsync(message.Id);
        Assert.Null(afterFirst.ProcessedAt);
        Assert.Equal(1, afterFirst.AttemptCount);
        Assert.Contains("bus unavailable", afterFirst.LastError!, StringComparison.Ordinal);

        // Still pending, so the next tick picks it up again.
        await DrainAsync(new RecordingPublisher(_ => true));

        var afterSecond = await ReadAsync(message.Id);
        Assert.Equal(2, afterSecond.AttemptCount);
    }

    [Fact]
    public async Task One_Bad_Message_Does_Not_Hold_Up_The_Batch()
    {
        // Availability over strict ordering: a single unpublishable message must
        // not stop everything behind it from being delivered.
        await MarkEverythingProcessedAsync();

        var now = DateTimeOffset.UtcNow;
        var poison = NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-3));
        var healthy = NewMessage(PostgresFixture.AlphaTenantId, now.AddMinutes(-2));
        await InsertAsync(poison, healthy);

        var publisher = new RecordingPublisher(message => message.Id == poison.Id);
        var published = await DrainAsync(publisher);

        Assert.Equal(1, published);
        Assert.Contains(publisher.Published, message => message.Id == healthy.Id);
        Assert.NotNull((await ReadAsync(healthy.Id)).ProcessedAt);
        Assert.Null((await ReadAsync(poison.Id)).ProcessedAt);
    }

    private static OutboxMessage NewMessage(Guid tenantId, DateTimeOffset occurredAt) =>
        OutboxMessage.Create(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            eventType: $"DrainEvt_{Guid.NewGuid():N}",
            payload: "{}",
            occurredAt: occurredAt.ToUniversalTime());

    private async Task<int> DrainAsync(IOutboxPublisher publisher, int batchSize = 100)
    {
        // Mirrors how the hosted service resolves its dependencies: a scope with
        // no ambient tenant, exactly as a background thread has.
        var services = new ServiceCollection();
        services.AddScoped(_ => fixture.CreateContext(new TenantContext()));
        services.AddScoped(_ => publisher);

        await using var provider = services.BuildServiceProvider();

        var processor = new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions { BatchSize = batchSize }),
            NullLogger<OutboxProcessor>.Instance);

        return await processor.DrainOnceAsync(CancellationToken.None);
    }

    private async Task InsertAsync(params OutboxMessage[] messages)
    {
        await using var context = fixture.CreateContext(new TenantContext());
        context.OutboxMessages.AddRange(messages);
        await context.SaveChangesAsync();
    }

    private async Task<OutboxMessage> ReadAsync(Guid id)
    {
        await using var context = fixture.CreateContext(new TenantContext());
        return await context.OutboxMessages.IgnoreQueryFilters().SingleAsync(message => message.Id == id);
    }

    private async Task MarkProcessedAsync(Guid id)
    {
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

    private sealed class RecordingPublisher : IOutboxPublisher
    {
        private readonly Func<OutboxMessage, bool>? failWhen;

        public RecordingPublisher(Func<OutboxMessage, bool>? failWhen = null) => this.failWhen = failWhen;

        public List<OutboxMessage> Published { get; } = [];

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            if (failWhen?.Invoke(message) == true)
            {
                throw new InvalidOperationException("bus unavailable");
            }

            Published.Add(message);
            return Task.CompletedTask;
        }
    }
}
