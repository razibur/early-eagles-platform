using EarlyEagles.Application.Behaviors;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Application;

/// <summary>
/// Registers Application-layer services. Called from EarlyEagles.Api during host
/// composition so the API project never has to know about Mediator / FluentValidation
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
        // Pipeline behaviors run in declared order, outermost first:
        //   1. LoggingBehavior     — always logs enter / exit / failure
        //   2. ValidationBehavior  — runs FluentValidation; throws ValidationException
        //                            before the handler if any rule fails
        // TransactionBehavior (P0-024.3) and AuditBehavior (P0-024.4) are written and
        // unit-tested but NOT registered here yet — they depend on IUnitOfWork (P0-032.2)
        // and IAuditService (P0-047.1) respectively. When fully wired, the pipeline order
        // becomes [Logging, Audit, Validation, Transaction] — Audit sits OUTSIDE the
        // transaction so failed commands still leave an audit trail.
        services.AddMediator(options =>
        {
            options.Assemblies = [applicationAssembly];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
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
