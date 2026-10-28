using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SupportDesk.IntegrationTests.Auth;

/// <summary>Tokens the API did not issue, or should no longer accept.</summary>
internal static class ForgedTokens
{
    private const string Issuer = "SupportDesk.Api";
    private const string Audience = "SupportDesk.Web";

    /// <summary>Correct in every way except the key it was signed with.</summary>
    public static string SignedWithAnotherKey() =>
        Create("someone-elses-key-that-is-long-enough-for-hs256", DateTime.UtcNow.AddHours(1));

    /// <summary>Signed with the right key, but expired well beyond the allowed clock skew.</summary>
    public static string Expired() =>
        Create(SupportDeskApiFactory.SigningKey, DateTime.UtcNow.AddMinutes(-5));

    private static string Create(string key, DateTime expiresAtUtc) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = expiresAtUtc.AddHours(-8),
            NotBefore = expiresAtUtc.AddHours(-8),
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "4")]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        });
}
