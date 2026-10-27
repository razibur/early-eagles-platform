namespace EarlyEagles.Tests.Integration.Messaging;

/// <summary>
/// A test that needs the Service Bus emulator (P0-050.3). Skipped unless
/// <c>EE_SERVICEBUS_EMULATOR</c> holds its connection string.
/// </summary>
/// <remarks>
/// The emulator only starts after someone accepts its licence and SQL Server's
/// (<c>ACCEPT_EULA=Y</c> in <c>.env</c>, docker-compose.yml). That acceptance
/// belongs to whoever runs it, so the suite never starts the emulator itself.
/// Run with:
/// <c>docker compose --profile bus up -d</c>, then
/// <c>EE_SERVICEBUS_EMULATOR="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"</c>.
/// </remarks>
public sealed class ServiceBusEmulatorFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "EE_SERVICEBUS_EMULATOR";

    public ServiceBusEmulatorFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Skip = $"Set {ConnectionStringVariable} to the Service Bus emulator's connection string to run.";
        }
    }

    public static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionStringVariable);
}
