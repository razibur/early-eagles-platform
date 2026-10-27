using Azure.Messaging.ServiceBus;
using EarlyEagles.Infrastructure.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Infrastructure.Messaging;

/// <summary>
/// The first consumer on the bus (P0-052): reads the <c>payments</c>
/// subscription of the <c>domain-events</c> topic, which the P0-051 filter
/// limits to <c>Subject = PaymentConfirmed</c>, and logs each event.
/// </summary>
/// <remarks>
/// <para>
/// Today it only proves the receive path: subscription, filter, contract
/// properties, settle. What a confirmed payment *does* (receipts, the dues
/// ledger) arrives with dues tracking in Phase 1 and plugs in at
/// <see cref="HandleAsync"/>.
/// </para>
/// <para>
/// Messages are completed only after <see cref="HandleAsync"/> returns, so a
/// crash mid-handle redelivers (up to the subscription's MaxDeliveryCount,
/// then dead-letters). Handlers must therefore be idempotent on
/// <c>MessageId</c>, the outbox row id.
/// </para>
/// </remarks>
public sealed class PaymentConfirmedHandler : BackgroundService
{
    public const string SubscriptionName = "payments";

    private readonly ServiceBusClient client;
    private readonly ServiceBusOptions options;
    private readonly ILogger<PaymentConfirmedHandler> logger;

    public PaymentConfirmedHandler(ServiceBusClient client, IOptions<ServiceBusOptions> options, ILogger<PaymentConfirmedHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.client = client;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <summary>Handles one received message. Public so tests can drive it without a bus.</summary>
    public Task HandleAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.ApplicationProperties.TryGetValue(ServiceBusOutboxPublisher.TenantIdProperty, out var tenantId);
        message.ApplicationProperties.TryGetValue(ServiceBusOutboxPublisher.EventTypeProperty, out var eventType);

        PaymentConfirmedHandlerLog.Received(
            logger,
            eventType as string ?? message.Subject,
            message.MessageId,
            tenantId as string ?? "unknown",
            message.DeliveryCount);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateProcessor(
            options.TopicName,
            SubscriptionName,
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 1 });

        processor.ProcessMessageAsync += async args =>
        {
            await HandleAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
        };
        processor.ProcessErrorAsync += args =>
        {
            PaymentConfirmedHandlerLog.ReceiveFailed(logger, args.ErrorSource.ToString(), args.EntityPath, args.Exception);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken).ConfigureAwait(false);
        PaymentConfirmedHandlerLog.Started(logger, options.TopicName, SubscriptionName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }

        await processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
