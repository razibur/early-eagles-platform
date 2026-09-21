using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// Drains the outbox (P0-050.1). The dispatcher (P0-049.2) only ever writes
/// rows — deliberately, so a bus outage can never roll back a legitimate
/// command. Something has to come along afterwards and deliver them; this is
/// that something.
/// </summary>
/// <remarks>
/// <para>
/// This task is the poll half: find the pending rows, oldest first, in batches.
/// Handing them to Service Bus and stamping <see cref="OutboxMessage.ProcessedAt"/>
/// is P0-050.2. Until that lands, every tick re-reads the same batch — expected,
/// not a leak.
/// </para>
/// <para>
/// It runs on its own DI scope per tick, which means its
/// <see cref="ApplicationDbContext"/> has no ambient tenant and its query filter
/// would match nothing. The read opts out with <c>IgnoreQueryFilters()</c>: the
/// outbox is the one place that legitimately spans tenants, because a single
/// process delivers for all of them. That is the same platform-work escape hatch
/// the pre-auth tenant lookup uses [<c>docs/runbooks/tenant-isolation-tests.md §7</c>],
/// and the reason it is safe here is that the processor only moves rows onto the
/// bus — it never answers a request, so there is no caller to leak to.
/// </para>
/// <para>
/// The query is written to match the partial index from P0-049.1
/// (<c>IX_outbox_messages_pending ON (occurred_at) WHERE processed_at IS NULL</c>),
/// so the working set stays proportional to the backlog rather than to the
/// table, which processed rows come to dominate.
/// </para>
/// </remarks>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly OutboxOptions options;
    private readonly ILogger<OutboxProcessor> logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.scopeFactory = scopeFactory;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <summary>
    /// Runs one drain on its own scope: read the pending batch oldest first,
    /// publish each message, stamp the outcome, save once. Returns how many
    /// were published. Public because it is the unit of work a tick performs —
    /// the loop calls it on a timer, and tests call it directly rather than
    /// racing one.
    /// </summary>
    public async Task<int> DrainOnceAsync(CancellationToken cancellationToken)
    {
        // One scope for the whole drain, not one per read: the messages have to
        // stay tracked by the context that will save their stamps.
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();

        var pending = await ReadPendingAsync(context, options.BatchSize, cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        OutboxProcessorLog.FoundPending(logger, pending.Count);

        var published = 0;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.MarkProcessed(DateTimeOffset.UtcNow);
                published++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown mid-batch. Whatever was stamped before this point is
                // still worth saving, so break rather than abandon the save.
                break;
            }
            catch (Exception exception)
            {
                // One unpublishable message must not hold up the rest of the
                // batch. The row stays pending with its attempt count bumped,
                // so the next tick retries it and a message that never succeeds
                // is visible in the table rather than invisibly looping.
                // Trade-off: while one message keeps failing, later ones are
                // delivered ahead of it, so strict ordering is not guaranteed
                // across a failure. Availability is worth more here than order,
                // and a dead-letter policy for poison messages comes with the
                // real bus implementation.
                message.RecordFailure(Describe(exception));
                OutboxProcessorLog.PublishFailed(logger, exception, message.EventType, message.Id);
            }
        }

        // A single save for the batch: the stamps are bookkeeping about work
        // already done, so there is nothing to roll back if a later message
        // fails — and one round trip beats one per message.
        await context.SaveChangesAsync(cancellationToken);

        OutboxProcessorLog.Drained(logger, published, pending.Count - published);

        return published;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            OutboxProcessorLog.Disabled(logger);
            return;
        }

        OutboxProcessorLog.Started(logger, options.PollInterval, options.BatchSize);

        // WaitForNextTickAsync waits the interval *before* the first tick, so a
        // host that starts and stops inside one interval never touches the
        // database — which is what keeps short-lived test hosts quiet.
        using var timer = new PeriodicTimer(options.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                await DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A tick that fails must not take the loop down with it. The
                // database being briefly unreachable is an ordinary event, and a
                // processor that exits on the first one stops delivering for the
                // whole process lifetime — silently, because nothing restarts a
                // completed BackgroundService.
                OutboxProcessorLog.PollFailed(logger, exception);
            }
        }

        OutboxProcessorLog.Stopped(logger);
    }

    private static Task<List<OutboxMessage>> ReadPendingAsync(
        ApplicationDbContext context,
        int batchSize,
        CancellationToken cancellationToken) =>
        context.OutboxMessages
            .IgnoreQueryFilters()
            .Where(message => message.ProcessedAt == null)
            .OrderBy(message => message.OccurredAt)
            .ThenBy(message => message.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    // Type plus message, not the stack trace: traces belong in the log
    // pipeline, while the row only has to say enough to recognise the failure
    // when reading the table.
    private static string Describe(Exception exception) =>
        $"{exception.GetType().Name}: {exception.Message}";
}
