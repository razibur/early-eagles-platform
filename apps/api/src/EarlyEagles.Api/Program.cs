using EarlyEagles.Api.Auditing;
using EarlyEagles.Api.ExceptionHandling;
using EarlyEagles.Api.Health;
using EarlyEagles.Api.Observability;
using EarlyEagles.Api.OpenApi;
using EarlyEagles.Api.Tenancy;
using EarlyEagles.Api.Webhooks;
using EarlyEagles.Application;
using EarlyEagles.Infrastructure;
using EarlyEagles.Infrastructure.Messaging;
using EarlyEagles.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddDocumentTransformer<TenantResolutionTransformer>();
    options.AddOperationTransformer<TenantHeaderOperationTransformer>();
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandling();
builder.Services.AddTransient<TenantResolutionMiddleware>();
builder.Services.AddTransient<TelemetryEnrichmentMiddleware>();

// Readiness checks behind /ready (P0-026.2). Each has its own timeout so one
// slow dependency fails the probe quickly instead of hanging it. The bus is
// checked only where one is configured; without it the outbox just logs.
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>(PostgresHealthCheck.Name, timeout: TimeSpan.FromSeconds(5));
if (EarlyEagles.Infrastructure.DependencyInjection.IsMessageBusConfigured(builder.Configuration))
{
    healthChecks.AddCheck<ServiceBusHealthCheck>(ServiceBusHealthCheck.Name, timeout: TimeSpan.FromSeconds(5));
}

var app = builder.Build();

// UseExceptionHandler is registered first so it can catch exceptions from any
// later middleware. With no path argument it picks up the IExceptionHandler
// instances registered via AddExceptionHandler<T>.
app.UseExceptionHandler();

// /openapi/v1.json (Microsoft.AspNetCore.OpenApi) and /swagger UI are gated on
// non-production. Swashbuckle.AspNetCore.SwaggerUI consumes the doc Microsoft
// generates — we don't pull in Swashbuckle's own generator. Production traffic
// must never see the API surface listed; tenant docs ship out-of-band.
if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "EarlyEagles API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "EarlyEagles API — " + app.Environment.EnvironmentName;
    });
}

app.UseHttpsRedirection();

// Tenant resolution runs before any handler so downstream code (EF query
// filter in P0-032.2, Mediator behaviors, feature endpoints) can rely on
// ITenantContext having been populated for subdomain-bearing requests. In
// Production a missing subdomain -> 400 and an unknown slug -> 404 (P0-031.3);
// outside Production misses pass through unresolved for dev-host convenience.
app.UseMiddleware<TenantResolutionMiddleware>();

// Tag the request's trace span with tenant (and later user) ids (P0-089.2).
app.UseMiddleware<TelemetryEnrichmentMiddleware>();

app.MapHealthEndpoints();
app.MapTenancyEndpoints();
app.MapTenantMembersEndpoints();

// Clerk webhook receiver (P0-036.1); anonymous, authenticated by its Svix
// signature (P0-037).
app.MapClerkWebhookEndpoints();

// Audit log query (P0-048.1). Outside Production only until it is restricted
// to tenant admins (P0-048.2, needs Clerk role claims from P0-038).
if (!app.Environment.IsProduction())
{
    app.MapAuditEventsEndpoints();
}

app.Run();

public partial class Program
{
}
