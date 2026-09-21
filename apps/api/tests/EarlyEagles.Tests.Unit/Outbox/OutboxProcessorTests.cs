using EarlyEagles.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Unit.Outbox;

// P0-050.1 — the loop's survival rules, which is what a background service
// actually gets wrong. Nothing restarts a BackgroundService whose ExecuteAsync
// returned, so an unhandled tick failure would stop delivery for the rest of
// the process lifetime without a single error after the first. These run
// without a database: the scope factory throws instead of connecting.
//
// Every test waits for a log line before asserting. StartAsync only *schedules*
// ExecuteAsync — stopping straight afterwards can cancel it before the body
// ever runs, which makes a start/stop pair prove nothing.
public class OutboxProcessorTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_Disabled_Processor_Never_Touches_The_Database()
    {
        var scopeFactory = new ThrowingScopeFactory();
        var logger = new RecordingLogger();
        using var processor = NewProcessor(scopeFactory, logger, new OutboxOptions { Enabled = false });

        await processor.StartAsync(CancellationToken.None);
        await logger.WaitFor(entry => entry.Level == LogLevel.Warning).WaitAsync(SignalTimeout);
        await processor.StopAsync(CancellationToken.None);

        // Rows keep accumulating; the kill switch pauses delivery, it does not
        // drop anything.
        Assert.Equal(0, scopeFactory.Attempts);
    }

    [Fact]
    public async Task The_First_Tick_Waits_A_Full_Interval()
    {
        // PeriodicTimer waits before its first tick, which is what keeps a host
        // that starts and stops quickly — every WebApplicationFactory test —
        // from opening a connection it never needed.
        var scopeFactory = new ThrowingScopeFactory();
        var logger = new RecordingLogger();
        using var processor = NewProcessor(
            scopeFactory,
            logger,
            new OutboxOptions { PollInterval = TimeSpan.FromMinutes(5) });

        await processor.StartAsync(CancellationToken.None);
        await logger.WaitFor(IsStarted).WaitAsync(SignalTimeout);
        await processor.StopAsync(CancellationToken.None);

        // The loop demonstrably ran — and still never polled.
        Assert.Equal(0, scopeFactory.Attempts);
    }

    [Fact]
    public async Task A_Failing_Tick_Does_Not_Stop_The_Loop()
    {
        // The load-bearing one: a second logged failure proves the loop ticked
        // again after the first blew up, rather than exiting quietly.
        var scopeFactory = new ThrowingScopeFactory();
        var logger = new RecordingLogger();
        using var processor = NewProcessor(
            scopeFactory,
            logger,
            new OutboxOptions { PollInterval = TimeSpan.FromMilliseconds(20) });

        var failures = 0;
        var secondFailure = logger.WaitFor(entry =>
            entry.Level == LogLevel.Error && Interlocked.Increment(ref failures) >= 2);

        await processor.StartAsync(CancellationToken.None);
        await secondFailure.WaitAsync(SignalTimeout);

        Assert.False(processor.ExecuteTask!.IsCompleted);
        Assert.True(scopeFactory.Attempts >= 2);

        await processor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stopping_Ends_The_Loop_Cleanly()
    {
        var scopeFactory = new ThrowingScopeFactory();
        var logger = new RecordingLogger();
        using var processor = NewProcessor(
            scopeFactory,
            logger,
            new OutboxOptions { PollInterval = TimeSpan.FromMinutes(5) });

        await processor.StartAsync(CancellationToken.None);
        await logger.WaitFor(IsStarted).WaitAsync(SignalTimeout);
        await processor.StopAsync(CancellationToken.None);

        // Cancellation is shutdown, not a fault: it must not surface as one.
        Assert.True(processor.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("stopped", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStarted(LogEntry entry) =>
        entry.Message.Contains("started", StringComparison.OrdinalIgnoreCase);

    private static OutboxProcessor NewProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger,
        OutboxOptions options) =>
        new(scopeFactory, Options.Create(options), logger);

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        private int attempts;

        public int Attempts => Volatile.Read(ref attempts);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("the database is unreachable");
        }
    }

    private sealed class RecordingLogger : ILogger<OutboxProcessor>
    {
        private readonly List<LogEntry> entries = [];
        private readonly List<(Func<LogEntry, bool> Match, TaskCompletionSource Signal)> waiters = [];

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (entries)
                {
                    return [.. entries];
                }
            }
        }

        /// <summary>
        /// Completes once an entry matching <paramref name="match"/> has been
        /// logged — the handshake that replaces sleeping and hoping.
        /// </summary>
        public Task WaitFor(Func<LogEntry, bool> match)
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (entries)
            {
                if (entries.Any(match))
                {
                    return Task.CompletedTask;
                }

                waiters.Add((match, signal));
            }

            return signal.Task;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var entry = new LogEntry(logLevel, formatter(state, exception));
            List<TaskCompletionSource> ready = [];

            lock (entries)
            {
                entries.Add(entry);

                for (var i = waiters.Count - 1; i >= 0; i--)
                {
                    if (waiters[i].Match(entry))
                    {
                        ready.Add(waiters[i].Signal);
                        waiters.RemoveAt(i);
                    }
                }
            }

            foreach (var signal in ready)
            {
                signal.TrySetResult();
            }
        }
    }
}
