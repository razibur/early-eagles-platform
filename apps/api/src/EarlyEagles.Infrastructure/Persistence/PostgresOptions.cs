namespace EarlyEagles.Infrastructure.Persistence;

/// <summary>How the API proves who it is to Postgres.</summary>
public enum PostgresAuthentication
{
    /// <summary>
    /// The password in the connection string. Local docker-compose Postgres and
    /// the Testcontainers database in tests.
    /// </summary>
    Password = 0,

    /// <summary>
    /// A Microsoft Entra access token fetched through a <c>TokenCredential</c>
    /// (the Container App's managed identity in Azure, <c>az login</c> on a
    /// laptop). Azure Postgres has password auth disabled (P0-058.1), so this
    /// is the only way in there. P0-062.2.1.
    /// </summary>
    Entra = 1,
}

/// <summary>
/// Postgres connection settings beyond the connection string, bound from the
/// <c>Postgres</c> configuration section. The connection string itself stays
/// in <c>ConnectionStrings:Postgres</c>.
/// </summary>
public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    /// <summary>Defaults to <see cref="PostgresAuthentication.Password"/> so local dev needs no setting.</summary>
    public PostgresAuthentication Authentication { get; set; } = PostgresAuthentication.Password;
}
