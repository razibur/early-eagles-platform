using Azure.Messaging.ServiceBus;
using EarlyEagles.Infrastructure.Messaging;
using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Integration.Messaging;

// P0-052.2 — publish a PaymentConfirmed through the real publisher and watch
// the hosted handler log it off the emulator's payments subscription.
// Same collection as OutboxToServiceBusTests: both read `payments`, and run
// in parallel they would take each other's messages.
[Collection(PostgresCollectionDefinition.Name)]
public class PaymentConfirmedHandlerTests
{
    [ServiceBusEmulatorFact]
    public async Task Published_PaymentConfirmed_Is_Logged_By_The_Handler()
    {
        await using var client = new ServiceBusClient(ServiceBusEmulatorFactAttribute.ConnectionString);
        var logger = new SignallingLogger();
        using var handler = new PaymentConfirmedHandler(client, Options.Create(new ServiceBusOptions()), logger);

        await handler.StartAsync(CancellationToken.None);
        try
        {
            var row = OutboxMessage.Create(
                Guid.NewGuid(),
                PostgresFixture.AlphaTenantId,
                "EarlyEagles.Domain.Payments.PaymentConfirmed",
                """{"amount":15}""",
                DateTimeOffset.UtcNow);
            await using var sender = client.CreateSender("domain-events");
            await new ServiceBusOutboxPublisher(sender).PublishAsync(row, CancellationToken.None);

            // Handshake, not a sleep: wait for the handler's own log line.
            var line = await logger.WaitFor(row.Id.ToString()).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Contains("EarlyEagles.Domain.Payments.PaymentConfirmed", line, StringComparison.Ordinal);
            Assert.Contains(PostgresFixture.AlphaTenantId.ToString(), line, StringComparison.Ordinal);
        }
        finally
        {
            await handler.StopAsync(CancellationToken.None);
        }
    }

    private sealed class SignallingLogger : ILogger<PaymentConfirmedHandler>
    {
        private readonly List<string> lines = [];
        private readonly List<(string Needle, TaskCompletionSource<string> Signal)> waiters = [];

        public Task<string> WaitFor(string needle)
        {
            var signal = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (lines)
            {
                var existing = lines.Find(l => l.Contains(needle, StringComparison.Ordinal));
                if (existing is not null)
                {
                    return Task.FromResult(existing);
                }

                waiters.Add((needle, signal));
            }

            return signal.Task;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            var line = formatter(state, exception);
            lock (lines)
            {
                lines.Add(line);
                foreach (var waiter in waiters.Where(w => line.Contains(w.Needle, StringComparison.Ordinal)).ToList())
                {
                    waiters.Remove(waiter);
                    waiter.Signal.TrySetResult(line);
                }
            }
        }
    }
}
