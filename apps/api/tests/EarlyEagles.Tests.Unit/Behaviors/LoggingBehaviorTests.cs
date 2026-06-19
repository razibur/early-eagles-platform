using EarlyEagles.Application.Behaviors;
using Mediator;
using Microsoft.Extensions.Logging;

namespace EarlyEagles.Tests.Unit.Behaviors;

public class LoggingBehaviorTests
{
    [Fact]
    public async Task Happy_Path_Logs_Enter_And_Exit_And_Returns_Response()
    {
        var logger = new CapturingLogger<LoggingBehavior<SampleRequest, string>>();
        var behavior = new LoggingBehavior<SampleRequest, string>(logger);
        var request = new SampleRequest("ping");

        var response = await behavior.Handle(
            request,
            (_, _) => ValueTask.FromResult("pong"),
            CancellationToken.None);

        Assert.Equal("pong", response);
        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Information, e.Level));
        Assert.Contains("Handling SampleRequest", logger.Entries[0].Message);
        Assert.Contains("Handled SampleRequest", logger.Entries[1].Message);
    }

    [Fact]
    public async Task Exception_From_Handler_Is_Logged_At_Error_And_Rethrown()
    {
        var logger = new CapturingLogger<LoggingBehavior<SampleRequest, string>>();
        var behavior = new LoggingBehavior<SampleRequest, string>(logger);
        var boom = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(
                new SampleRequest("ping"),
                (_, _) => throw boom,
                CancellationToken.None));

        Assert.Same(boom, thrown);
        var errorEntry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Same(boom, errorEntry.Exception);
        Assert.Contains("Failed SampleRequest", errorEntry.Message);
    }

    [Fact]
    public async Task Null_Next_Throws_Argument_Null_Exception()
    {
        var logger = new CapturingLogger<LoggingBehavior<SampleRequest, string>>();
        var behavior = new LoggingBehavior<SampleRequest, string>(logger);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.Handle(new SampleRequest("ping"), null!, CancellationToken.None));
    }

    private sealed record SampleRequest(string Value) : IRequest<string>;

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);
}
