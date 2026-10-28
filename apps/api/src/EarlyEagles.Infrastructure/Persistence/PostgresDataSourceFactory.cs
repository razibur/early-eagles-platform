using Azure.Core;
using Npgsql;

namespace EarlyEagles.Infrastructure.Persistence;

/// <summary>
/// Builds the one <see cref="NpgsqlDataSource"/> the API connects through
/// (P0-062.2.1). With <see cref="PostgresAuthentication.Entra"/> the data source
/// signs in with an Entra access token instead of a password.
/// </summary>
/// <remarks>
/// A data source is built here rather than configured through EF's
/// <c>ConfigureDataSource</c>: EF doesn't track that callback when it caches
/// data sources, and Npgsql's docs recommend passing a pre-built data source to
/// <c>UseNpgsql</c> instead.
/// </remarks>
public static class PostgresDataSourceFactory
{
    /// <summary>The token audience for Azure Database for PostgreSQL.</summary>
    public const string EntraTokenScope = "https://ossrdbms-aad.database.windows.net/.default";

    /// <summary>
    /// How long Npgsql reuses a token before asking the credential again. Short on
    /// purpose. The credential caches the token in memory, so asking is cheap, and
    /// it only renews the token in its last few minutes. If Npgsql held on for an
    /// hour, it could keep using a token that expired partway through.
    /// </summary>
    public static readonly TimeSpan TokenRefreshInterval = TimeSpan.FromMinutes(5);

    /// <summary>Retry interval after a failed token fetch.</summary>
    public static readonly TimeSpan TokenRetryInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Npgsql only cancels the refresh callback when the data source is disposed, so
    /// each fetch gets its own timeout. A hung IMDS call would otherwise stall
    /// every later refresh.
    /// </summary>
    public static readonly TimeSpan TokenFetchTimeout = TimeSpan.FromSeconds(30);

    public static NpgsqlDataSource Create(
        string connectionString,
        PostgresAuthentication authentication,
        Func<TokenCredential> credentialFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(credentialFactory);

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        // Npgsql prefers GSS (Kerberos) encryption by default, so every new
        // connection loads libgssapi_krb5. The chiseled runtime image doesn't
        // ship it and logs an error per connection; nothing here uses
        // Kerberos (local password, Azure password or Entra token). Off unless
        // the connection string asks for it explicitly.
        if (!connectionString.Contains("gss", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        if (authentication == PostgresAuthentication.Entra)
        {
            // A password left in the string would be silently ignored in favour of
            // the token. That's the kind of leftover that ends up in a commit, so
            // refuse it outright.
            if (!string.IsNullOrEmpty(builder.ConnectionStringBuilder.Password))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Postgres must not contain a Password when Postgres:Authentication is Entra.");
            }

            // Azure Postgres maps the Entra principal by role name, so the user is
            // required: the managed identity's name (id-ee-api-<env>-eus).
            if (string.IsNullOrWhiteSpace(builder.ConnectionStringBuilder.Username))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Postgres must set Username to the Entra principal's Postgres role when Postgres:Authentication is Entra.");
            }

            var credential = credentialFactory();
            builder.UsePeriodicPasswordProvider(
                (_, cancellationToken) => GetEntraTokenAsync(credential, cancellationToken),
                TokenRefreshInterval,
                TokenRetryInterval);
        }

        return builder.Build();
    }

    /// <summary>Fetches one Postgres access token; the token string is the password.</summary>
    public static async ValueTask<string> GetEntraTokenAsync(TokenCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TokenFetchTimeout);

        var token = await credential.GetTokenAsync(
            new TokenRequestContext([EntraTokenScope]),
            timeout.Token).ConfigureAwait(false);

        return token.Token;
    }
}
