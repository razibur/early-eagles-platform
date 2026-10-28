using System.Reflection;

namespace EarlyEagles.Api;

/// <summary>
/// Facts about the running build, shared by <c>/health</c> and telemetry so
/// both report the same version: <c>1.0.0+&lt;commit&gt;</c> (the Dockerfile
/// passes the commit in as SourceRevisionId).
/// </summary>
internal static class ApplicationInfo
{
    public const string ServiceName = "early-eagles-api";

    public static readonly string Version = ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = typeof(ApplicationInfo).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
