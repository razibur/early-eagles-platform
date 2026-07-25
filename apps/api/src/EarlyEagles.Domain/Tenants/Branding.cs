using System.Text.RegularExpressions;
using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// White-label appearance for a tenant — logo + color pair driving the CSS-
/// variable theme renderer (see P2-011 / P2-012). Immutable: replace the whole
/// VO via <see cref="Tenant.SetBranding"/> rather than mutating fields. Hex
/// inputs are normalised to lowercase so structural equality is case-insensitive.
/// </summary>
public sealed partial class Branding : ValueObject
{
    public const int LogoUrlMaxLength = 2048;

    public Branding(string logoUrl, string primaryColor, string accentColor)
    {
        LogoUrl = ValidateLogoUrl(logoUrl);
        PrimaryColor = ValidateHexColor(primaryColor, nameof(primaryColor));
        AccentColor = ValidateHexColor(accentColor, nameof(accentColor));
    }

    public string LogoUrl { get; }

    public string PrimaryColor { get; }

    public string AccentColor { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LogoUrl;
        yield return PrimaryColor;
        yield return AccentColor;
    }

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex HexColorRegex();

    private static string ValidateLogoUrl(string logoUrl)
    {
        ArgumentNullException.ThrowIfNull(logoUrl);

        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            throw new ArgumentException("Logo URL must not be empty or whitespace.", nameof(logoUrl));
        }

        if (logoUrl.Length > LogoUrlMaxLength)
        {
            throw new ArgumentException(
                $"Logo URL must be at most {LogoUrlMaxLength} characters; got {logoUrl.Length}.",
                nameof(logoUrl));
        }

        if (!Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Logo URL must be an absolute URI.", nameof(logoUrl));
        }

        if (uri.Scheme is not "http" and not "https")
        {
            throw new ArgumentException(
                $"Logo URL scheme must be http or https; got '{uri.Scheme}'.",
                nameof(logoUrl));
        }

        return logoUrl;
    }

    private static string ValidateHexColor(string value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);

        if (!HexColorRegex().IsMatch(value))
        {
            throw new ArgumentException(
                "Color must be a hex value in #RGB, #RRGGBB, or #RRGGBBAA format.",
                paramName);
        }

        return value.ToLowerInvariant();
    }
}
