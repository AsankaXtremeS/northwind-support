namespace SupportDesk.Application.Auth;

/// <summary>
/// How sign-in tokens are issued and checked, bound from the <c>Jwt</c> section of configuration.
/// The signing key is never in source: it comes from appsettings.Development.json locally and
/// from the <c>Jwt__SigningKey</c> environment variable elsewhere. Checked at startup by
/// <see cref="JwtOptionsValidator"/>.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Symmetric key the API signs and validates tokens with.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>How long a token is valid. There is no refresh token, so this is one session.</summary>
    public int LifetimeMinutes { get; set; }
}
