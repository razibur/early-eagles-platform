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
        //   2. AuditBehavior       — records every ICommand dispatch (success or
        //                            failure) via IAuditService. Sits OUTSIDE the
        //                            transaction on purpose (see AuditBehavior doc)
        //                            so a rolled-back command still leaves an audit
        //                            trail. Queries pass through.
        //   3. ValidationBehavior  — runs FluentValidation; throws ValidationException
        //                            before the handler if any rule fails. Audit
        //                            records the resulting failure.
        // TransactionBehavior (P0-024.3) is written and unit-tested but NOT registered
        // here yet — it depends on IUnitOfWork, which lands with the first real
        // command. When fully wired the pipeline becomes
        // [Logging, Audit, Validation, Transaction].
        services.AddMediator(options =>
        {
            options.Assemblies = [applicationAssembly];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
                typeof(AuditBehavior<,>),
                typeof(ValidationBehavior<,>),
            ];
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
