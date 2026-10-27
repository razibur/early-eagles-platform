using EarlyEagles.Api.ExceptionHandling;
using EarlyEagles.Api.Health;
using EarlyEagles.Api.OpenApi;
using EarlyEagles.Api.Tenancy;
using EarlyEagles.Application;
using EarlyEagles.Infrastructure;
using EarlyEagles.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

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

// Readiness checks behind /ready (P0-026.2). Each has its own timeout so one
// slow dependency fails the probe quickly instead of hanging it. Service Bus
// joins when the real publisher does (P0-050.2.2).
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>(PostgresHealthCheck.Name, timeout: TimeSpan.FromSeconds(5));

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

app.MapHealthEndpoints();
app.MapTenancyEndpoints();
app.MapTenantMembersEndpoints();

app.Run();

public partial class Program
{
}
