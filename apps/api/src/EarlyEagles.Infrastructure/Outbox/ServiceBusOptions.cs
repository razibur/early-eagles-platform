namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// The <c>ServiceBus</c> configuration section (P0-050.2.2). Where to publish:
/// either <see cref="FullyQualifiedNamespace"/> (Azure, signed in with the
/// app's <c>TokenCredential</c>; SAS keys are disabled on the namespace) or a
/// <c>ConnectionStrings:ServiceBus</c> (the local emulator). With neither, the
/// outbox keeps the logging publisher and nothing is sent.
/// </summary>
public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public const string ConnectionStringName = "ServiceBus";

    /// <summary>e.g. <c>sbns-ee-dev-eus-abcde.servicebus.windows.net</c>; set by environment.bicep.</summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>The topic every domain event goes to (P0-051).</summary>
    public string TopicName { get; set; } = "domain-events";
}
