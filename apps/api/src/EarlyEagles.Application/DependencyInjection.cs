using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Application;

/// <summary>
/// Registers Application-layer services. Called from EarlyEagles.Api during host
/// composition so the API project never has to know about MediatR / FluentValidation
/// / Mapperly directly — that's an Application-layer concern.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var applicationAssembly = typeof(AssemblyMarker).Assembly;

        // martinothamar/Mediator — source-generated mediator. Scans the Application
        // assembly for IRequestHandler<,> / INotificationHandler<> at compile time.
        services.AddMediator(options =>
        {
            options.Assemblies = [applicationAssembly];
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        // FluentValidation — discover IValidator<T> implementations in the Application
        // assembly. Pipeline behavior (P0-024.2) will run them ahead of handler dispatch.
        services.AddValidatorsFromAssembly(applicationAssembly, ServiceLifetime.Scoped);

        // Mapperly mappers are source-generated partial classes; they are registered
        // explicitly when added (no assembly scan needed). Placeholder until the first
        // mapper lands in Phase 1.
        return services;
    }
}
