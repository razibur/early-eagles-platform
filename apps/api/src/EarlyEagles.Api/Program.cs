using EarlyEagles.Api.ExceptionHandling;
using EarlyEagles.Api.Health;
using EarlyEagles.Api.OpenApi;
using EarlyEagles.Api.Tenancy;
using EarlyEagles.Application;
using EarlyEagles.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddDocumentTransformer<TenantResolutionTransformer>();
    options.AddOperationTransformer<TenantHeaderOperationTransformer>();
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddExceptionHandling();
builder.Services.AddTransient<TenantResolutionMiddleware>();

// HealthChecks pipeline for the /ready endpoint. Concrete checks land later:
//   .AddDbContextCheck<ApplicationDbContext>("database")  // P0-032.2
//   .AddAzureServiceBusQueue(...)                         // P0-058.4
builder.Services.AddHealthChecks();

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
// ITenantContext having been populated for subdomain-bearing requests.
// Misses (no subdomain, unknown slug) leave the context unresolved here —
// P0-031.3 layers on the 404 / 400 responses.
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapHealthEndpoints();

app.Run();

public partial class Program
{
}
