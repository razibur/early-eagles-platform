using Azure.Messaging.ServiceBus;
using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Integration.Messaging;

// P0-050.3 — outbox row → drain → ServiceBusOutboxPublisher → the emulator's
// domain-events topic → the payments subscription (infra/local/servicebus/
// Config.json). Proves the P0-051 contract end to end: routing by Subject,
// the properties consumers rely on, and duplicate detection on MessageId.
[Collection(PostgresCollectionDefinition.Name)]
public class OutboxToServiceBusTests
{
    private const string PaymentConfirmed = "EarlyEagles.Domain.Payments.PaymentConfirmed";

    private static readonly TimeSpan ReceiveWait = TimeSpan.FromSeconds(10);

    private readonly PostgresFixture fixture;

    public OutboxToServiceBusTests(PostgresFixture fixture) => this.fixture = fixture;

    [ServiceBusEmulatorFact]
    public async Task Drained_Row_Arrives_On_The_Payments_Subscription_With_The_Contract_Properties()
    {
        await using var client = new ServiceBusClient(ServiceBusEmulatorFactAttribute.ConnectionString);
        await using var receiver = client.CreateReceiver("domain-events", "payments");
        await PurgeAsync(receiver);

        var row = await QueueAsync(PaymentConfirmed, """{"amount":15,"method":"zelle"}""");
        await DrainAsync(client);

        var received = await ReceiveOneAsync(receiver, row.Id);
        Assert.NotNull(received);
        Assert.Equal("PaymentConfirmed", received!.Subject);
        Assert.Equal("application/json", received.ContentType);
        Assert.Equal(PaymentConfirmed, received.ApplicationProperties["EventType"]);
        Assert.Equal(PostgresFixture.AlphaTenantId.ToString(), received.ApplicationProperties["TenantId"]);
        Assert.Equal("""{"amount":15,"method":"zelle"}""", received.Body.ToString());

        // And the drain stamped it, so it won't be sent again.
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.NotNull((await db.OutboxMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == row.Id)).ProcessedAt);
    }

    [ServiceBusEmulatorFact]
    public async Task Other_Events_Are_Filtered_Out_Of_The_Payments_Subscription()
    {
        await using var client = new ServiceBusClient(ServiceBusEmulatorFactAttribute.ConnectionString);
        await using var receiver = client.CreateReceiver("domain-events", "payments");
        await PurgeAsync(receiver);

        var row = await QueueAsync("EarlyEagles.Domain.Tenants.TenantRenamed", "{}");
        await DrainAsync(client);

        Assert.Null(await ReceiveOneAsync(receiver, row.Id));
    }

    [ServiceBusEmulatorFact]
    public async Task A_Resend_With_The_Same_MessageId_Is_Dropped_By_Duplicate_Detection()
    {
        // The drain is at-least-once: dying between send and stamp re-sends.
        // The topic must swallow the repeat.
        await using var client = new ServiceBusClient(ServiceBusEmulatorFactAttribute.ConnectionString);
        await using var receiver = client.CreateReceiver("domain-events", "payments");
        await using var sender = client.CreateSender("domain-events");
        await PurgeAsync(receiver);

        var row = OutboxMessage.Create(Guid.NewGuid(), PostgresFixture.AlphaTenantId, PaymentConfirmed, "{}", DateTimeOffset.UtcNow);
        var publisher = new ServiceBusOutboxPublisher(sender);
        await publisher.PublishAsync(row, CancellationToken.None);
        await publisher.PublishAsync(row, CancellationToken.None);

        Assert.NotNull(await ReceiveOneAsync(receiver, row.Id));
        Assert.Null(await ReceiveOneAsync(receiver, row.Id));
    }

    // Empties the subscription of anything an earlier run left behind.
    private static async Task PurgeAsync(ServiceBusReceiver receiver)
    {
        while (true)
        {
            var batch = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(1));
            if (batch.Count == 0)
            {
                return;
            }

            foreach (var message in batch)
            {
                await receiver.CompleteMessageAsync(message);
            }
        }
    }

    // Receives until the message with this id shows up or the wait runs out;
    // anything else is completed and ignored.
    private static async Task<ServiceBusReceivedMessage?> ReceiveOneAsync(ServiceBusReceiver receiver, Guid id)
    {
        var deadline = DateTimeOffset.UtcNow + ReceiveWait;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
            if (message is null)
            {
                continue;
            }

            await receiver.CompleteMessageAsync(message);
            if (message.MessageId == id.ToString())
            {
                return message;
            }
        }

        return null;
    }

    private async Task<OutboxMessage> QueueAsync(string eventType, string payload)
    {
        await using var db = fixture.CreateContext(new TenantContext());

        // Other tests leave pending rows behind; stamp them so this drain only
        // sends what this test queued.
        await db.Database.ExecuteSqlRawAsync("UPDATE outbox_messages SET processed_at = now() WHERE processed_at IS NULL");

        var row = OutboxMessage.Create(Guid.NewGuid(), PostgresFixture.AlphaTenantId, eventType, payload, DateTimeOffset.UtcNow);
        db.OutboxMessages.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private async Task DrainAsync(ServiceBusClient client)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => fixture.CreateContext(new TenantContext()));
        services.AddSingleton(client.CreateSender("domain-events"));
        services.AddScoped<IOutboxPublisher, ServiceBusOutboxPublisher>();
        await using var provider = services.BuildServiceProvider();

        var processor = new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions()),
            NullLogger<OutboxProcessor>.Instance);

        Assert.Equal(1, await processor.DrainOnceAsync(CancellationToken.None));
    }
}
