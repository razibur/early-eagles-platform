using System.Text.Json;
using Azure.Messaging.ServiceBus;
using EarlyEagles.Application.Abstractions.Email;
using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Application.Payments;
using EarlyEagles.Domain.Payments;
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

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task HandleAsync_Logs_The_Event_Type_Message_Id_Tenant_And_Delivery()
    {
        var logger = new RecordingLogger();
        await using var client = new ServiceBusClient(EmulatorConnectionString);
        await using var services = new ServiceCollection().BuildServiceProvider();
        var handler = new PaymentConfirmedHandler(client, Options.Create(new ServiceBusOptions()), services.GetRequiredService<IServiceScopeFactory>(), logger);
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
        await using var services = new ServiceCollection().BuildServiceProvider();
        var handler = new PaymentConfirmedHandler(client, Options.Create(new ServiceBusOptions()), services.GetRequiredService<IServiceScopeFactory>(), logger);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"), messageId: "m1", subject: "PaymentConfirmed");

        await handler.HandleAsync(message, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("PaymentConfirmed", entry.Message, StringComparison.Ordinal);
        Assert.Contains("tenant unknown", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_Emails_The_Admins_About_A_Real_Payment_Event()
    {
        var sender = new RecordingEmailSender();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IEmailSender>(sender);
        serviceCollection.AddSingleton<IPaymentEmailDirectory>(new OneAdminDirectory());
        serviceCollection.AddScoped<PaymentConfirmedNotifier>();
        await using var services = serviceCollection.BuildServiceProvider();
        await using var client = new ServiceBusClient(EmulatorConnectionString);
        var handler = new PaymentConfirmedHandler(client, Options.Create(new ServiceBusOptions()), services.GetRequiredService<IServiceScopeFactory>(), new RecordingLogger());
        var confirmed = new PaymentConfirmed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "user_1", 1500);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(JsonSerializer.Serialize(confirmed, WebJson)),
            messageId: "m2",
            subject: "PaymentConfirmed");

        await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal("payment-confirmed/m2", Assert.Single(sender.Sent).IdempotencyKey);
    }

    [Fact]
    public async Task HandleAsync_Throws_On_A_Body_That_Is_Not_Json_So_It_Dead_Letters()
    {
        await using var client = new ServiceBusClient(EmulatorConnectionString);
        await using var services = new ServiceCollection().BuildServiceProvider();
        var handler = new PaymentConfirmedHandler(client, Options.Create(new ServiceBusOptions()), services.GetRequiredService<IServiceScopeFactory>(), new RecordingLogger());
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("not json"), messageId: "m3", subject: "PaymentConfirmed");

        await Assert.ThrowsAsync<JsonException>(() => handler.HandleAsync(message, CancellationToken.None));
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

    private sealed class OneAdminDirectory : IPaymentEmailDirectory
    {
        public Task<PaymentEmailFacts?> FindAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken) =>
            Task.FromResult<PaymentEmailFacts?>(new PaymentEmailFacts("Club", ["admin@example.com"], "Name", new DateOnly(2026, 11, 8), "Hall", 1500, "Sender"));
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.FromResult<string?>(null);
        }
    }

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
