using EarlyEagles.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Unit.Configuration;

// P0-036.1, P0-035.2, P0-044 — malformed Clerk and Stripe settings stop the
// host at startup. IStartupValidator runs exactly the ValidateOnStart checks
// the host runs before it starts. These were WebApplicationFactory tests, but
// a host that fails to start races the factory: it can be disposed before the
// factory reads it, and the test then sees ObjectDisposedException instead.
public class StartupSettingsValidationTests
{
    private const string Issuer = "https://test-instance.clerk.accounts.dev";
    private const string Origin = "https://earlyeagles.club";

    [Fact]
    public void Well_Formed_Settings_Pass()
    {
        Validate(new()
        {
            ["Clerk:WebhookSigningSecret"] = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw",
            ["Clerk:Issuer"] = Issuer,
            ["Clerk:AuthorizedParties:0"] = Origin,
            ["Stripe:WebhookSigningSecret"] = "whsec_stripe_test_secret",
        });
    }

    [Fact]
    public void Unset_Settings_Pass() => Validate([]);

    [Theory]
    [InlineData("Clerk:WebhookSigningSecret", "not-a-whsec-secret", "Clerk:WebhookSigningSecret")]
    [InlineData("Clerk:Issuer", "http://test-instance.clerk.accounts.dev", "Clerk:Issuer")]
    [InlineData("Clerk:Issuer", "https://test-instance.clerk.accounts.dev/", "Clerk:Issuer")]
    [InlineData("Clerk:Issuer", "not a url", "Clerk:Issuer")]
    [InlineData("Clerk:AuthorizedParties:0", "https://earlyeagles.club/app", "Clerk:AuthorizedParties")]
    [InlineData("Clerk:AuthorizedParties:0", "earlyeagles.club", "Clerk:AuthorizedParties")]
    [InlineData("Stripe:WebhookSigningSecret", "sk_test_not_a_webhook_secret", "Stripe:WebhookSigningSecret")]
    public void A_Malformed_Setting_Fails_Startup_Validation(string key, string value, string expectedKey)
    {
        var exception = Assert.Throws<OptionsValidationException>(() => Validate(new() { [key] = value }));

        Assert.Contains(expectedKey, exception.Message, StringComparison.Ordinal);
    }

    private static void Validate(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
    }
}
