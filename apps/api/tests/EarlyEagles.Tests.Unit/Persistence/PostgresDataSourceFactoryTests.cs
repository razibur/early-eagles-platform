using Azure.Core;
using EarlyEagles.Infrastructure;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EarlyEagles.Tests.Unit.Persistence;

// P0-062.2.1 — the API signs in to Azure Postgres with an Entra token instead of a
// password. These pin the wiring without a server: which mode fetches a token, what
// audience it asks for, and which connection strings are refused before they can
// fail confusingly at first connect.
public class PostgresDataSourceFactoryTests
{
    private const string EntraConnectionString =
        "Host=psql-ee-dev-eus-001.postgres.database.azure.com;Database=earlyeagles;Username=id-ee-api-dev-eus;SslMode=Require";

    private const string PasswordConnectionString =
        "Host=localhost;Port=55432;Database=earlyeagles;Username=postgres;Password=postgres";

    [Theory]
    [InlineData(PostgresAuthentication.Password, PasswordConnectionString)]
    [InlineData(PostgresAuthentication.Entra, EntraConnectionString)]
    public void Gss_Encryption_Is_Off_By_Default(PostgresAuthentication authentication, string connectionString)
    {
        // P0-084: the chiseled runtime image has no libgssapi_krb5, and Npgsql's
        // default (Prefer) loads it on every connection and logs an error.
        using var dataSource = PostgresDataSourceFactory.Create(connectionString, authentication, () => new FakeCredential());

        Assert.Equal(GssEncryptionMode.Disable, new NpgsqlConnectionStringBuilder(dataSource.ConnectionString).GssEncryptionMode);
    }

    [Fact]
    public void An_Explicit_Gss_Setting_In_The_Connection_String_Is_Kept()
    {
        using var dataSource = PostgresDataSourceFactory.Create(
            PasswordConnectionString + ";Gss Encryption Mode=Require",
            PostgresAuthentication.Password,
            () => new FakeCredential());

        Assert.Equal(GssEncryptionMode.Require, new NpgsqlConnectionStringBuilder(dataSource.ConnectionString).GssEncryptionMode);
    }

    [Fact]
    public void Password_Mode_Never_Creates_A_Credential()
    {
        // Local dev has no Azure login. If password mode created a
        // DefaultAzureCredential anyway, a laptop without `az login` would pay its
        // probe timeouts for nothing.
        var credentialsCreated = 0;

        using var dataSource = PostgresDataSourceFactory.Create(
            PasswordConnectionString,
            PostgresAuthentication.Password,
            () =>
            {
                credentialsCreated++;
                return new FakeCredential();
            });

        Assert.Equal(0, credentialsCreated);
        Assert.Equal("postgres", new NpgsqlConnectionStringBuilder(dataSource.ConnectionString).Username);
    }

    [Fact]
    public void Entra_Mode_Creates_The_Credential_Once()
    {
        var credentialsCreated = 0;

        using var dataSource = PostgresDataSourceFactory.Create(
            EntraConnectionString,
            PostgresAuthentication.Entra,
            () =>
            {
                credentialsCreated++;
                return new FakeCredential();
            });

        Assert.Equal(1, credentialsCreated);
    }

    [Fact]
    public void Entra_Mode_Refuses_A_Connection_String_With_A_Password()
    {
        // Npgsql would quietly prefer the token, leaving a stale secret in config
        // that looks like it matters.
        var error = Assert.Throws<InvalidOperationException>(() => PostgresDataSourceFactory.Create(
            EntraConnectionString + ";Password=leftover",
            PostgresAuthentication.Entra,
            () => new FakeCredential()));

        Assert.Contains("Password", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Entra_Mode_Requires_A_Username()
    {
        // Azure Postgres matches the token to a role by name. Without one, Npgsql
        // falls back to the OS user name, which fails at connect with an auth
        // error that doesn't point here.
        var error = Assert.Throws<InvalidOperationException>(() => PostgresDataSourceFactory.Create(
            "Host=psql.example;Database=earlyeagles;SslMode=Require",
            PostgresAuthentication.Entra,
            () => new FakeCredential()));

        Assert.Contains("Username", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Token_Is_Requested_For_The_Postgres_Audience_And_Used_As_The_Password()
    {
        var credential = new FakeCredential();

        var password = await PostgresDataSourceFactory.GetEntraTokenAsync(credential, CancellationToken.None);

        Assert.Equal(FakeCredential.Token, password);
        var request = Assert.Single(credential.Requests);
        Assert.Equal(["https://ossrdbms-aad.database.windows.net/.default"], request.Scopes);
    }

    [Fact]
    public void Token_Is_Refreshed_Well_Inside_The_Credential_Renewal_Window()
    {
        // The credential renews a token only in its last few minutes. If Npgsql
        // held a token for longer than that window, it could keep using one that
        // had already expired.
        Assert.InRange(PostgresDataSourceFactory.TokenRefreshInterval, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        Assert.InRange(PostgresDataSourceFactory.TokenRetryInterval, TimeSpan.Zero, PostgresDataSourceFactory.TokenRefreshInterval);
    }

    [Fact]
    public void AddInfrastructure_Binds_Entra_Mode_And_Uses_The_Registered_Credential()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = EntraConnectionString,
                ["Postgres:Authentication"] = "Entra",
            })
            .Build();

        var services = new ServiceCollection();
        var credential = new FakeCredential();
        services.AddSingleton<TokenCredential>(credential);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        // A password in the string would have thrown here; reaching the data source
        // proves Entra mode was bound and the fake (not DefaultAzureCredential) used.
        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        Assert.Equal("id-ee-api-dev-eus", new NpgsqlConnectionStringBuilder(dataSource.ConnectionString).Username);
        Assert.Same(credential, provider.GetRequiredService<TokenCredential>());
    }

    [Fact]
    public void AddInfrastructure_Defaults_To_Password_Mode()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = PasswordConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        // Password mode with Entra's rules would throw on a string that has a password.
        Assert.NotNull(provider.GetRequiredService<NpgsqlDataSource>());
    }

    private sealed class FakeCredential : TokenCredential
    {
        public const string Token = "fake-entra-token";

        public List<TokenRequestContext> Requests { get; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Requests.Add(requestContext);
            return new AccessToken(Token, DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
