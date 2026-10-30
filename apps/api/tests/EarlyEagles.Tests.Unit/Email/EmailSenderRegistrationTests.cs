using EarlyEagles.Application.Abstractions.Email;
using EarlyEagles.Infrastructure;
using EarlyEagles.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Unit.Email;

// P0-053.1 — which IEmailSender the host gets, and which Resend settings stop it.
public class EmailSenderRegistrationTests
{
    private const string ApiKey = "re_test_123456789";
    private const string From = "Early Eagles <no-reply@earlyeagles.club>";

    [Fact]
    public void Without_An_Api_Key_The_Logging_Sender_Is_Registered()
    {
        using var provider = Build([]);

        Assert.IsType<LoggingEmailSender>(provider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void With_An_Api_Key_Resend_Is_Registered()
    {
        using var provider = Build(new() { ["Resend:ApiKey"] = ApiKey, ["Resend:From"] = From });

        Assert.IsType<ResendEmailSender>(provider.GetRequiredService<IEmailSender>());
    }

    [Theory]
    [InlineData("sk_live_wrong_prefix", From, "Resend:ApiKey")]
    [InlineData(ApiKey, null, "Resend:From")]
    [InlineData(ApiKey, "not an address", "Resend:From")]
    public void Malformed_Settings_Fail_Validation(string apiKey, string? from, string expectedKey)
    {
        using var provider = Build(new() { ["Resend:ApiKey"] = apiKey, ["Resend:From"] = from });

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ResendOptions>>().Value);
        Assert.Contains(expectedKey, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no-reply@earlyeagles.club")]
    [InlineData("Early Eagles <no-reply@earlyeagles.club>")]
    public void Bare_And_Named_Senders_Are_Valid(string from) => Assert.True(ResendOptions.IsValidFrom(from));

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.BuildServiceProvider();
    }
}
