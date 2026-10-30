using System.Net.Mail;

namespace EarlyEagles.Infrastructure.Email;

/// <summary>
/// The <c>Resend</c> configuration section (P0-053.1). The API key is a
/// secret: Key Vault in Azure, user-secrets / <c>.env</c> on a laptop
/// (<c>Resend__ApiKey</c>), never appsettings. Unset means no email leaves the
/// process: <see cref="LoggingEmailSender"/> is registered instead.
/// </summary>
public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>The prefix Resend puts on every API key.</summary>
    public const string ApiKeyPrefix = "re_";

    /// <summary>Resend's API root; requests go to <c>emails</c> under it.</summary>
    public static readonly Uri BaseAddress = new("https://api.resend.com/");

    /// <summary>An API key with sending access, <c>re_…</c>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The sender, on a domain verified in Resend, e.g.
    /// <c>Early Eagles &lt;no-reply@earlyeagles.club&gt;</c>. Required once
    /// <see cref="ApiKey"/> is set.
    /// </summary>
    public string? From { get; set; }

    /// <summary>True when <paramref name="from"/> is <c>a@b.c</c> or <c>Name &lt;a@b.c&gt;</c>.</summary>
    public static bool IsValidFrom(string? from) =>
        !string.IsNullOrWhiteSpace(from) && MailAddress.TryCreate(from, out _);
}
