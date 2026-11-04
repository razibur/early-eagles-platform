using System.Reflection;
using EarlyEagles.Application.Abstractions.Persistence;
using Mediator;
using NetArchTest.Rules;

namespace EarlyEagles.Tests.Unit.Architecture;

// Phase 0 gate: "an audit row is written for every state-changing command".
// AuditBehavior audits every ICommand, so the gate holds as long as state only
// changes through commands. These rules keep it that way: every message is a
// command or a query, queries can't reach the write side, and the API host
// never touches the write side except by sending a command.
public class AuditCoverageRulesTests
{
    private static readonly Assembly ApplicationAssembly = typeof(Application.AssemblyMarker).Assembly;

    // Repositories and the unit of work: the ports that save state.
    private static readonly Type[] WritePorts = ApplicationAssembly.GetTypes()
        .Where(t => t.IsInterface && (t.Name.EndsWith("Repository", StringComparison.Ordinal) || t == typeof(IUnitOfWork)))
        .ToArray();

    [Fact]
    public void The_Write_Ports_Are_Found()
    {
        // Guards the rules below against passing vacuously after a rename.
        Assert.Contains(WritePorts, t => t.Name == "ITenantRepository");
        Assert.Contains(typeof(IUnitOfWork), WritePorts);
    }

    [Fact]
    public void Every_Message_Is_A_Command_Or_A_Query()
    {
        // A plain IRequest would skip AuditBehavior, which only audits commands.
        var offenders = ApplicationAssembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IMessage).IsAssignableFrom(t))
            .Where(t => !IsCommand(t) && !Implements(t, typeof(IQuery<>)))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, "Not a command or a query: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Query_Handlers_Do_Not_Take_Write_Ports()
    {
        // A query that saved state would change data without an audit row.
        var offenders = ApplicationAssembly.GetTypes()
            .Where(t => !t.IsAbstract && Implements(t, typeof(IQueryHandler<,>)))
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters()), (t, p) => (Handler: t, Parameter: p.ParameterType))
            .Where(x => WritePorts.Contains(x.Parameter))
            .Select(x => $"{x.Handler.Name}({x.Parameter.Name})")
            .ToList();

        Assert.True(offenders.Count == 0, "Query handlers with write ports: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_Api_Host_Changes_State_Only_Through_Commands()
    {
        var result = Types
            .InAssembly(typeof(Program).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(WritePorts.Select(t => t.FullName!).ToArray())
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "API types using write ports directly: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }

    private static bool IsCommand(Type type) => typeof(ICommand).IsAssignableFrom(type) || Implements(type, typeof(ICommand<>));

    private static bool Implements(Type type, Type openGeneric) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);
}
