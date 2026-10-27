using Azure.Messaging.ServiceBus;
using EarlyEagles.Infrastructure;
using EarlyEagles.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Tests.Unit.Outbox;

public class ServiceBusOutboxPublisherTests
{
    private const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private static readonly Guid TenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void CreateMessage_Follows_The_P0_051_Contract()
    {
        var row = OutboxMessage.Create(
            Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"),
            TenantId,
            "EarlyEagles.Domain.Payments.PaymentConfirmed",
            """{"amount":15}""",
            DateTimeOffset.UtcNow);

        var message = ServiceBusOutboxPublisher.CreateMessage(row);

        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", message.MessageId);
        Assert.Equal("PaymentConfirmed", message.Subject);
        Assert.Equal("application/json", message.ContentType);
        Assert.Equal("EarlyEagles.Domain.Payments.PaymentConfirmed", message.ApplicationProperties["EventType"]);
        Assert.Equal(TenantId.ToString(), message.ApplicationProperties["TenantId"]);
        Assert.Equal("""{"amount":15}""", message.Body.ToString());
    }

    [Theory]
    [InlineData("EarlyEagles.Domain.Payments.PaymentConfirmed", "PaymentConfirmed")]
    [InlineData("EarlyEagles.Domain.Tenants.Tenant+Created", "Created")]
    [InlineData("NoNamespace", "NoNamespace")]
    public void ShortName_Takes_The_Last_Type_Segment(string eventType, string expected)
    {
        Assert.Equal(expected, ServiceBusOutboxPublisher.ShortName(eventType));
    }

    [Fact]
    public async Task PublishAsync_Sends_The_Mapped_Message()
    {
        var sender = new RecordingSender();
        var row = OutboxMessage.Create(Guid.NewGuid(), TenantId, "X.Y.Happened", "{}", DateTimeOffset.UtcNow);

        await new ServiceBusOutboxPublisher(sender).PublishAsync(row, CancellationToken.None);

        var sent = Assert.Single(sender.Sent);
        Assert.Equal(row.Id.ToString(), sent.MessageId);
        Assert.Equal("Happened", sent.Subject);
    }

    [Fact]
    public async Task PublishAsync_Lets_A_Send_Failure_Escape_So_The_Drain_Records_It()
    {
        var sender = new RecordingSender { Failure = new ServiceBusException("down", ServiceBusFailureReason.ServiceCommunicationProblem) };
        var row = OutboxMessage.Create(Guid.NewGuid(), TenantId, "X.Y.Happened", "{}", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ServiceBusException>(() => new ServiceBusOutboxPublisher(sender).PublishAsync(row, CancellationToken.None));
    }

    [Fact]
    public void Without_Bus_Configuration_The_Logging_Publisher_Is_Registered()
    {
        using var provider = BuildProvider([]);
        using var scope = provider.CreateScope();

        Assert.IsType<LoggingOutboxPublisher>(scope.ServiceProvider.GetRequiredService<IOutboxPublisher>());
    }

    [Fact]
    public async Task With_An_Emulator_Connection_String_The_Service_Bus_Publisher_Is_Registered()
    {
        await using var provider = BuildProvider(new() { ["ConnectionStrings:ServiceBus"] = EmulatorConnectionString });
        using var scope = provider.CreateScope();

        Assert.IsType<ServiceBusOutboxPublisher>(scope.ServiceProvider.GetRequiredService<IOutboxPublisher>());
        Assert.Equal("domain-events", provider.GetRequiredService<ServiceBusSender>().EntityPath);
    }

    [Fact]
    public async Task With_A_Namespace_The_Service_Bus_Publisher_Uses_The_Configured_Topic()
    {
        await using var provider = BuildProvider(new()
        {
            ["ServiceBus:FullyQualifiedNamespace"] = "sbns-ee-dev-eus-abcde.servicebus.windows.net",
            ["ServiceBus:TopicName"] = "other-topic",
        });
        using var scope = provider.CreateScope();

        Assert.IsType<ServiceBusOutboxPublisher>(scope.ServiceProvider.GetRequiredService<IOutboxPublisher>());
        var sender = provider.GetRequiredService<ServiceBusSender>();
        Assert.Equal("other-topic", sender.EntityPath);
        Assert.Equal("sbns-ee-dev-eus-abcde.servicebus.windows.net", sender.FullyQualifiedNamespace);
    }

    // Constructing the client and sender opens no connection, so these
    // registrations resolve without a bus or a credential round-trip.
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class RecordingSender : ServiceBusSender
    {
        public List<ServiceBusMessage> Sent { get; } = [];

        public Exception? Failure { get; init; }

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
