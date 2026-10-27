using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EarlyEagles.Infrastructure.Messaging;

/// <summary>
/// Readiness check for the bus (P0-026.2.2). Creating a message batch opens
/// the outbox sender's AMQP link to the topic, with the app's own
/// credentials, and sends nothing. A wrong namespace, missing topic, revoked
/// Data Sender role or unreachable broker all fail it.
/// </summary>
/// <remarks>
/// Registered only when a bus is configured. Like the database check, its
/// description is fixed text because <c>/ready</c> is unauthenticated.
/// </remarks>
public sealed class ServiceBusHealthCheck : IHealthCheck
{
    public const string Name = "servicebus";

    private readonly ServiceBusSender sender;

    public ServiceBusHealthCheck(ServiceBusSender sender)
    {
        this.sender = sender;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            using var batch = await sender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Message bus reachable.");
        }
        catch (Exception ex) when (ex is ServiceBusException or AuthenticationFailedException or UnauthorizedAccessException or OperationCanceledException or TimeoutException or InvalidOperationException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Message bus unreachable.", ex);
        }
    }
}
