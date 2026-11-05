using System.Text.Json;
using Azure.Messaging.ServiceBus;
using EarlyEagles.Application.Payments;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Infrastructure.Messaging;

/// <summary>
/// The first consumer on the bus (P0-052): reads the <c>payments</c>
/// subscription of the <c>domain-events</c> topic, which the P0-051 filter
/// limits to <c>Subject = PaymentConfirmed</c>, logs each event and emails the
/// club's admins (P1-010).
/// </summary>
/// <remarks>
/// <para>
/// A message carrying no payment id has nothing to email about and is completed
/// after the log line. A body that isn't JSON throws, so it dead-letters rather
/// than vanishing.
/// </para>
/// <para>
/// Messages are completed only after <see cref="HandleAsync"/> returns, so a
/// crash mid-handle redelivers (up to the subscription's MaxDeliveryCount,
/// then dead-letters). Handlers must therefore be idempotent on
/// <c>MessageId</c>, the outbox row id: the email's idempotency key is built
/// from it.
/// </para>
/// </remarks>
public sealed class PaymentConfirmedHandler : BackgroundService
{
    public const string SubscriptionName = "payments";

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusClient client;
    private readonly ServiceBusOptions options;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<PaymentConfirmedHandler> logger;

    public PaymentConfirmedHandler(
        ServiceBusClient client,
        IOptions<ServiceBusOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentConfirmedHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.client = client;
        this.options = options.Value;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    /// <summary>Handles one received message. Public so tests can drive it without a bus.</summary>
    public async Task HandleAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
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

        var confirmed = message.Body.ToObjectFromJson<PaymentConfirmed>(PayloadOptions);
        if (confirmed is null || confirmed.PaymentId == Guid.Empty)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<PaymentConfirmedNotifier>()
            .NotifyAsync(confirmed, message.MessageId, cancellationToken)
            .ConfigureAwait(false);
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
