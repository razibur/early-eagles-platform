using Microsoft.Extensions.Logging;

namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// Source-generated logging methods for <see cref="OutboxProcessor"/>. Lives in
/// its own file so StyleCop's "one type per file" rule (SA1402) stays green.
/// </summary>
internal static partial class OutboxProcessorLog
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Outbox processor started; polling every {PollInterval} in batches of {BatchSize}")]
    public static partial void Started(ILogger logger, TimeSpan pollInterval, int batchSize);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "Outbox processor is disabled by configuration; queued events will not be delivered")]
    public static partial void Disabled(ILogger logger);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "Outbox processor stopped")]
    public static partial void Stopped(ILogger logger);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Debug, Message = "Outbox poll found {Count} pending message(s)")]
    public static partial void FoundPending(ILogger logger, int count);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Error,
        Message = "Outbox poll failed; retrying on the next tick")]
    public static partial void PollFailed(ILogger logger, Exception exception);
}
