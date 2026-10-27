using Azure.Messaging.ServiceBus;
using EarlyEagles.Infrastructure;
using EarlyEagles.Infrastructure.Messaging;
using EarlyEagles.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Unit.Messaging;

public class PaymentConfirmedHandlerTests
{
    private const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    [Fact]
    public async Task HandleAsync_Logs_The_Event_Type_Message_Id_Tenant_And_Delivery()
    {
        var logger = new RecordingLogger();
        await using var client = new ServiceBusClient(EmulatorConnectionString);
        var handler = new PaymentConfirmedHandler(client, Options.Create(new ServiceBusOptions()), logger);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "0f8fad5b-d9cb-469f-a165-70867728950e",
            subject: "PaymentConfirmed",
            deliveryCount: 2,
            properties: new Dictionary<string, object>
            {
                ["EventType"] = "EarlyEagles.Domain.Payments.PaymentConfirmed",
                ["TenantId"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            });

        await handler.HandleAsync(message, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(1301, entry.EventId);
        Assert.Contains("EarlyEagles.Domain.Payments.PaymentConfirmed", entry.Message, StringComparison.Ordinal);
        Assert.Contains("0f8fad5b-d9cb-469f-a165-70867728950e", entry.Message, StringComparison.Ordinal);
        Assert.Contains("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", entry.Message, StringComparison.Ordinal);
        Assert.Contains("delivery 2", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_Falls_Back_To_Subject_When_Properties_Are_Missing()
    {
        var logger = new RecordingLogger();
        await using var client = new ServiceBusClient(EmulatorConnectionString);
        var handler = new PaymentConfirmedHandler(client, Options.Create(new ServiceBusOptions()), logger);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"), messageId: "m1", subject: "PaymentConfirmed");

        await handler.HandleAsync(message, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("PaymentConfirmed", entry.Message, StringComparison.Ordinal);
        Assert.Contains("tenant unknown", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registered_Only_When_A_Bus_Is_Configured()
    {
        Assert.DoesNotContain(HostedServiceTypes([]), t => t == typeof(PaymentConfirmedHandler));
        Assert.Contains(HostedServiceTypes(new() { ["ConnectionStrings:ServiceBus"] = EmulatorConnectionString }), t => t == typeof(PaymentConfirmedHandler));
    }

    private static List<Type?> HostedServiceTypes(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .ToList();
    }

    private sealed record LogEntry(int EventId, string Message);

    private sealed class RecordingLogger : ILogger<PaymentConfirmedHandler>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Entries.Add(new LogEntry(eventId.Id, formatter(state, exception)));
        }
    }
}
