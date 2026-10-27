using Azure.Messaging.ServiceBus;
using EarlyEagles.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EarlyEagles.Tests.Unit.Messaging;

public class ServiceBusHealthCheckTests
{
    [Fact]
    public async Task Healthy_When_The_Sender_Link_Opens()
    {
        var result = await Check(new FakeSender());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Unhealthy_With_Fixed_Text_When_The_Broker_Fails()
    {
        var sender = new FakeSender
        {
            Failure = new ServiceBusException("sbns-secret.servicebus.windows.net refused", ServiceBusFailureReason.ServiceCommunicationProblem),
        };

        var result = await Check(sender);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Message bus unreachable.", result.Description);
        Assert.IsType<ServiceBusException>(result.Exception);
    }

    private static Task<HealthCheckResult> Check(ServiceBusSender sender)
    {
        var check = new ServiceBusHealthCheck(sender);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(ServiceBusHealthCheck.Name, check, HealthStatus.Unhealthy, tags: null),
        };
        return check.CheckHealthAsync(context, CancellationToken.None);
    }

    private sealed class FakeSender : ServiceBusSender
    {
        public Exception? Failure { get; init; }

        public override ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            return ValueTask.FromResult(ServiceBusModelFactory.ServiceBusMessageBatch(1024, []));
        }
    }
}
