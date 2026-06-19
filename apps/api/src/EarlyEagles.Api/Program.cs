using EarlyEagles.Api.ExceptionHandling;
using EarlyEagles.Api.Health;
using EarlyEagles.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddExceptionHandling();

var app = builder.Build();

// UseExceptionHandler is registered first so it can catch exceptions from any
// later middleware. With no path argument it picks up the IExceptionHandler
// instances registered via AddExceptionHandler<T>.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthEndpoints();

app.Run();

public partial class Program
{
}
