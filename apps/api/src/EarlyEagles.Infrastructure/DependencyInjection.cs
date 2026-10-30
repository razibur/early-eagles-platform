using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using EarlyEagles.Application.Abstractions.Auditing;
using EarlyEagles.Application.Abstractions.Email;
using EarlyEagles.Application.Abstractions.Messaging;
using EarlyEagles.Application.Abstractions.Persistence;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Auditing;
using EarlyEagles.Infrastructure.Email;
using EarlyEagles.Infrastructure.Identity;
using EarlyEagles.Infrastructure.Messaging;
using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Infrastructure.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EarlyEagles.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer services. Called from EarlyEagles.Api during
/// host composition so the API project never has to know about specific
/// implementations — composition is an infrastructure concern.
/// </summary>
public static class DependencyInjection
{
    // Placeholder connection until real per-environment configuration lands in
    // P0-033.4. AddDbContext / UseNpgsql never open a connection at registration,
    // so this keeps the host bootable (and the model buildable) without a live DB.
    // Also used by the design-time factory when no override env var is set.
    internal const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=earlyeagles;Username=postgres;Password=postgres";

    private static readonly string UserAgentVersion =
        typeof(DependencyInjection).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Single scoped TenantContext exposed under both interfaces. Both reads
        // and the (single) initialise call within one request hit the same
        // instance; the next request gets a fresh one. Tenant-resolution
        // middleware (P0-031.2) calls Initialize; everyone else consumes the
        // read interface.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(sp => sp.GetRequiredService<TenantContext>());

        // Slug → tenant for the resolution middleware (P0-031.5). Opens its own
        // context, so it never pins the request's to "no tenant".
        services.AddScoped<ITenantLookup, EfTenantLookup>();

        // ApplicationDbContext holds the tenant-isolation global query filter
        // (P0-032.2). Scoped so it snapshots ITenantContext per request.
        // One NpgsqlDataSource for the process (P0-062.2.1): it owns the
        // connection pool and, in Entra mode, the token refresh timer. Built
        // lazily, so the credential is only created when a context is.
        var connectionString = configuration.GetConnectionString("Postgres") ?? DefaultConnectionString;
        var postgresOptions = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>() ?? new PostgresOptions();

        // DefaultAzureCredential: the managed identity in Azure (it reads
        // AZURE_CLIENT_ID, set by containerApp.bicep), `az login` on a laptop.
        // TryAdd so tests can swap in a fake.
        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddSingleton(sp => PostgresDataSourceFactory.Create(
            connectionString,
            postgresOptions.Authentication,
            sp.GetRequiredService<TokenCredential>));
        services.AddDbContext<ApplicationDbContext>((sp, options) => options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

        // Audit service (P0-047.1). Scoped because it depends on the scoped
        // ITenantContext. It opens its own DbContext per call so audit writes
        // survive request-transaction rollbacks — see AuditService for why.
        services.AddScoped<IAuditService, AuditService>();

        // Audit log read side (P0-048). Scoped onto the request's context so the
        // tenant query filter applies.
        services.AddScoped<IAuditLogReader, EfAuditLogReader>();

        // Tenant aggregate write side and the platform-level Clerk lookup
        // (P0-036.2). Scoped onto the request's context.
        services.AddScoped<ITenantRepository, EfTenantRepository>();
        services.AddScoped<IClerkTenantDirectory, EfClerkTenantDirectory>();

        // Tenant member write side (P0-036.3.1), same request-scoped context.
        services.AddScoped<ITenantMemberRepository, EfTenantMemberRepository>();

        // Unit of work (P0-024.3) backing TransactionBehavior. Scoped so it
        // shares the request's ApplicationDbContext — the transaction it opens
        // covers everything that context saves.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Domain-event dispatcher (P0-049.2). Scoped onto the request's context
        // so staged outbox rows commit with that request's state change. The
        // save-time drain of aggregates runs through DomainEventOutboxInterceptor,
        // which the context registers itself; this registration is for handlers
        // that raise an event without an aggregate to hang it on.
        services.AddScoped<IDomainEventDispatcher, OutboxDomainEventDispatcher>();

        // Outbox drain (P0-050.1). Validated at startup rather than on first
        // tick: a bad PollInterval would otherwise surface ten seconds into
        // production as a background exception nobody is watching.
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .Validate(
                options => options.PollInterval > TimeSpan.Zero,
                $"{OutboxOptions.SectionName}:{nameof(OutboxOptions.PollInterval)} must be greater than zero.")
            .Validate(
                options => options.BatchSize is > 0 and <= OutboxOptions.MaxBatchSize,
                $"{OutboxOptions.SectionName}:{nameof(OutboxOptions.BatchSize)} must be between 1 and {OutboxOptions.MaxBatchSize}.")
            .ValidateOnStart();

        // Clerk webhooks (P0-036.1, P0-037). The signing secret is optional so
        // the API boots without Clerk, but a malformed one fails at startup
        // instead of rejecting every webhook later.
        services.AddOptions<ClerkOptions>()
            .Bind(configuration.GetSection(ClerkOptions.SectionName))
            .Validate(
                options => string.IsNullOrEmpty(options.WebhookSigningSecret) || SvixWebhookVerifier.IsValidSecret(options.WebhookSigningSecret),
                $"{ClerkOptions.SectionName}:{nameof(ClerkOptions.WebhookSigningSecret)} must look like '{SvixWebhookVerifier.SecretPrefix}<base64>'.")
            .Validate(
                options => string.IsNullOrEmpty(options.Issuer) || ClerkOptions.IsValidIssuer(options.Issuer),
                $"{ClerkOptions.SectionName}:{nameof(ClerkOptions.Issuer)} must be the Frontend API URL, e.g. 'https://clerk.earlyeagles.club' (https, no path or trailing slash).")
            .Validate(
                options => options.AuthorizedParties.All(ClerkOptions.IsValidOrigin),
                $"{ClerkOptions.SectionName}:{nameof(ClerkOptions.AuthorizedParties)} entries must be origins, e.g. 'https://earlyeagles.club' (no path or trailing slash).")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);

        AddEmailSender(services, configuration);
        AddOutboxPublisher(services, configuration);

        services.AddHostedService<OutboxProcessor>();

        return services;
    }

    /// <summary>
    /// True when configuration names a Service Bus (Azure namespace or emulator
    /// connection string). The API uses it to add the bus readiness check only
    /// where there is a bus to check (P0-026.2.2).
    /// </summary>
    public static bool IsMessageBusConfigured(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.IsNullOrWhiteSpace(configuration.GetConnectionString(ServiceBusOptions.ConnectionStringName))
            || !string.IsNullOrWhiteSpace(configuration[$"{ServiceBusOptions.SectionName}:{nameof(ServiceBusOptions.FullyQualifiedNamespace)}"]);
    }

    // Transactional email (P0-053.1). With an API key: Resend, over a typed
    // HttpClient carrying the key and the User-Agent Resend insists on. A key
    // without a valid From fails at startup. Without a key: the logging
    // stand-in, so a laptop runs every email path and sends nothing.
    private static void AddEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.SectionName))
            .Validate(
                options => string.IsNullOrEmpty(options.ApiKey) || options.ApiKey.StartsWith(ResendOptions.ApiKeyPrefix, StringComparison.Ordinal),
                $"{ResendOptions.SectionName}:{nameof(ResendOptions.ApiKey)} must look like '{ResendOptions.ApiKeyPrefix}…'.")
            .Validate(
                options => string.IsNullOrEmpty(options.ApiKey) || ResendOptions.IsValidFrom(options.From),
                $"{ResendOptions.SectionName}:{nameof(ResendOptions.From)} must be set to a sender on a verified domain, e.g. 'Early Eagles <no-reply@earlyeagles.club>'.")
            .ValidateOnStart();

        if (string.IsNullOrEmpty(configuration[$"{ResendOptions.SectionName}:{nameof(ResendOptions.ApiKey)}"]))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
            return;
        }

        services.AddHttpClient<IEmailSender, ResendEmailSender>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ResendOptions>>().Value;
            client.BaseAddress = ResendOptions.BaseAddress;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EarlyEagles", UserAgentVersion));
        });
    }

    // Where outbox rows go (P0-050.2.2), and who listens (P0-052). Azure: the namespace from config,
    // signed in with the app's TokenCredential (the namespace has SAS keys
    // disabled). Emulator: a connection string. Neither: the logging
    // publisher, so a laptop without the bus still runs; queued events are
    // then marked processed without reaching any bus.
    private static void AddOutboxPublisher(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ServiceBusOptions.SectionName).Get<ServiceBusOptions>() ?? new ServiceBusOptions();
        var connectionString = configuration.GetConnectionString(ServiceBusOptions.ConnectionStringName);

        if (!IsMessageBusConfigured(configuration))
        {
            services.AddScoped<IOutboxPublisher, LoggingOutboxPublisher>();
            return;
        }

        // One client and one sender for the process: both are thread-safe and
        // own the AMQP connection. The container disposes them at shutdown.
        services.AddSingleton(sp => string.IsNullOrWhiteSpace(connectionString)
            ? new ServiceBusClient(options.FullyQualifiedNamespace, sp.GetRequiredService<TokenCredential>())
            : new ServiceBusClient(connectionString));
        services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(options.TopicName));
        services.AddScoped<IOutboxPublisher, ServiceBusOutboxPublisher>();

        // Consumers (P0-052) share the client. Options are bound for them
        // through IOptions so tests and handlers see the same topic name.
        services.AddOptions<ServiceBusOptions>().Bind(configuration.GetSection(ServiceBusOptions.SectionName));
        services.AddHostedService<PaymentConfirmedHandler>();
    }
}
