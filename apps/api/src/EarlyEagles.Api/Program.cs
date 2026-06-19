using EarlyEagles.Api.ExceptionHandling;
using EarlyEagles.Api.Health;
using EarlyEagles.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddExceptionHandling();

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

app.MapHealthEndpoints();

app.Run();

public partial class Program
{
}
