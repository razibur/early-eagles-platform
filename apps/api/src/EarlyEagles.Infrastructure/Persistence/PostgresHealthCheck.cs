using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace EarlyEagles.Infrastructure.Persistence;

/// <summary>
/// Readiness check for the database (P0-026.2): borrows a pooled connection
/// from the app's <see cref="NpgsqlDataSource"/> and runs <c>SELECT 1</c>.
/// Going through the same data source as EF means it exercises what requests
/// use, including the Entra token in Azure (P0-062.2.1).
/// </summary>
/// <remarks>
/// <c>/ready</c> is unauthenticated, so the description is fixed text. The
/// exception (host names, auth errors) is attached to the result for logs,
/// never written to the response. Register it with a timeout: Npgsql's own
/// connect timeout is 15 s, far too slow for a readiness probe.
/// </remarks>
public sealed class PostgresHealthCheck : IHealthCheck
{
    public const string Name = "postgres";

    private readonly NpgsqlDataSource dataSource;

    public PostgresHealthCheck(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Database reachable.");
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Database unreachable.", ex);
        }
    }
}
