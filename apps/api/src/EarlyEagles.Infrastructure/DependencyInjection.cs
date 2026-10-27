using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using EarlyEagles.Application.Abstractions.Auditing;
using EarlyEagles.Application.Abstractions.Messaging;
using EarlyEagles.Application.Abstractions.Persistence;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Auditing;
using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // Placeholder tenant lookup — always returns null. Replaced by an
        // EF-backed implementation once a live database is wired (P0-033 / P0-034).
        // Scoped lifetime matches the future EF impl shape.
        services.AddScoped<ITenantLookup, NullTenantLookup>();

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

        AddOutboxPublisher(services, configuration);

        services.AddHostedService<OutboxProcessor>();

        return services;
    }

    // Where outbox rows go (P0-050.2.2). Azure: the namespace from config,
    // signed in with the app's TokenCredential (the namespace has SAS keys
    // disabled). Emulator: a connection string. Neither: the logging
    // publisher, so a laptop without the bus still runs; queued events are
    // then marked processed without reaching any bus.
    private static void AddOutboxPublisher(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ServiceBusOptions.SectionName).Get<ServiceBusOptions>() ?? new ServiceBusOptions();
        var connectionString = configuration.GetConnectionString(ServiceBusOptions.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString) && string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace))
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
    }
}
