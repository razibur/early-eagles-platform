using NetArchTest.Rules;

namespace EarlyEagles.Tests.Unit.Architecture;

public class CleanArchitectureRulesTests
{
    [Fact]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types
            .InAssembly(typeof(Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn("EarlyEagles.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types
            .InAssembly(typeof(Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn("EarlyEagles.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types
            .InAssembly(typeof(Application.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn("EarlyEagles.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    private static string FormatFailure(TestResult result)
    {
        if (result.FailingTypes is null || result.FailingTypes.Count == 0)
        {
            return "Architecture rule violated.";
        }

        var names = string.Join(", ", result.FailingTypes.Select(t => t.FullName));
        return $"Architecture rule violated by: {names}";
    }
}
