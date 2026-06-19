namespace EarlyEagles.Api.ExceptionHandling;

/// <summary>
/// Source-generated logging methods for <see cref="GlobalExceptionHandler"/>.
/// Separate file so StyleCop's one-type-per-file rule (SA1402) stays green.
/// </summary>
internal static partial class GlobalExceptionHandlerLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Error,
        Message = "Unhandled exception escaped to the global handler")]
    public static partial void UnhandledException(ILogger logger, Exception exception);
}
