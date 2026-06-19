using Microsoft.Extensions.Logging;

namespace EarlyEagles.Application.Behaviors;

/// <summary>
/// Source-generated logging methods for <see cref="LoggingBehavior{TMessage, TResponse}"/>.
/// Lives in its own file so StyleCop's "one type per file" rule (SA1402) stays green.
/// </summary>
internal static partial class LoggingBehaviorLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Handling {MessageType}")]
    public static partial void Handling(ILogger logger, string messageType);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Handled {MessageType} in {ElapsedMs}ms")]
    public static partial void Handled(ILogger logger, string messageType, long elapsedMs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Failed {MessageType} after {ElapsedMs}ms")]
    public static partial void Failed(ILogger logger, Exception exception, string messageType, long elapsedMs);
}
