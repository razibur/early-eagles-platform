using System.Collections.Concurrent;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Tests.Integration.Authentication;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Observability;

// P0-090 — log lines written while a tenant request runs carry TenantId and
// TenantSlug from the enrichment middleware's scope. Drives a real query
// through the Mediator pipeline, whose LoggingBehavior logs "Handling …".
// The endpoint is tenant-admin only (P0-048.2), so the request carries a signed
// admin session and the scope carries its user id too.
[Collection(PostgresCollectionDefinition.Name)]
public class TenantLogScopeTests
{
    private const string AlphaOrg = "org_scope_alpha";
    private const string UserId = "user_scope_check";

    private readonly PostgresFixture fixture;

    public TenantLogScopeTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Logs_Inside_A_Tenant_Request_Carry_The_Tenant_Scope()
    {
        var provider = new ScopeCapturingLoggerProvider();
        using var factory = new ScopeFactory(fixture.ConnectionString, provider);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{PostgresFixture.AlphaSlug}.localhost";

        var token = ClerkTestTokens.Create(UserId, "sess_scope_check", extra: ClerkTestTokens.ActiveOrganization(AlphaOrg, "admin"));

        var response = await client.SendAsync(ClerkTestTokens.Get("/audit-events", token));
        response.EnsureSuccessStatusCode();

        var handling = Assert.Single(provider.Entries, e => e.Message.StartsWith("Handling ListAuditEventsQuery", StringComparison.Ordinal));
        Assert.Equal(PostgresFixture.AlphaTenantId, handling.Scope["TenantId"]);
        Assert.Equal(PostgresFixture.AlphaSlug, handling.Scope["TenantSlug"]);
        Assert.Equal(UserId, handling.Scope["UserId"]);
    }

    private sealed record Entry(string Message, IReadOnlyDictionary<string, object?> Scope);

    private sealed class ScopeFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly ScopeCapturingLoggerProvider provider;

        public ScopeFactory(string connectionString, ScopeCapturingLoggerProvider provider)
        {
            this.connectionString = connectionString;
            this.provider = provider;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.AddProvider(provider));
            ClerkTestTokens.UseTestIssuer(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITenantLookup>();
                services.AddSingleton<ITenantLookup>(new AlphaLookup());
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }

    private sealed class AlphaLookup : ITenantLookup
    {
        public Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(slug == PostgresFixture.AlphaSlug ? new TenantLookupResult(PostgresFixture.AlphaTenantId, slug, AlphaOrg) : null);
    }

    // Records every log line with the key/value pairs of every scope active
    // when it was written, the way the JSON console and OTel exporters see them.
    private sealed class ScopeCapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

        public ConcurrentBag<Entry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ScopeCapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => owner.scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var scope = new Dictionary<string, object?>();
                owner.scopes.ForEachScope(
                    (value, bag) =>
                    {
                        if (value is IEnumerable<KeyValuePair<string, object?>> pairs)
                        {
                            foreach (var pair in pairs)
                            {
                                bag[pair.Key] = pair.Value;
                            }
                        }
                    },
                    scope);
                owner.Entries.Add(new Entry(formatter(state, exception), scope));
            }
        }
    }
}
