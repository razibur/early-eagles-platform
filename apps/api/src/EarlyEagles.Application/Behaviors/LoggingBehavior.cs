using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EarlyEagles.Application.Behaviors;

/// <summary>
/// Mediator pipeline behavior that logs every command / query as it enters and exits
/// the handler, along with elapsed duration. Exceptions thrown by the inner handler
/// are logged at Error level with stack trace and re-thrown unchanged.
///
/// Correlation comes from <see cref="Activity.Current"/>'s W3C TraceId — populated by
/// ASP.NET Core for inbound HTTP requests and by OpenTelemetry instrumentation. The
/// TraceId is attached as a structured log property via <see cref="ILogger.BeginScope"/>
/// so every nested log line within the handler also carries it.
///
/// Log calls use the <c>LoggerMessage</c> source generator (see <c>LoggingBehaviorLog</c>)
/// so this hot path allocates no <c>object[]</c> for arguments and skips message
/// formatting entirely when the log level is filtered out.
/// </summary>
public sealed class LoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    private readonly ILogger<LoggingBehavior<TMessage, TResponse>> logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    {
        this.logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var messageType = typeof(TMessage).Name;
        var correlationId = Activity.Current?.TraceId.ToString() ?? "no-trace";

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageType"] = messageType,
            ["CorrelationId"] = correlationId,
        });

        LoggingBehaviorLog.Handling(logger, messageType);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(message, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            LoggingBehaviorLog.Handled(logger, messageType, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LoggingBehaviorLog.Failed(logger, ex, messageType, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
