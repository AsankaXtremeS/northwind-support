using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Auth;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Presentation.Auth;

/// <summary>
/// Issues an HMAC-SHA256 signed JWT carrying the agent's id (<c>sub</c>), name and email.
/// The same <see cref="JwtOptions"/> configure validation, so issuing and checking cannot drift.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(AuthUserDto user)
    {
        var settings = options.Value;
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(settings.LifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(settings), SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expiresAtUtc);
    }

    /// <summary>The key both for signing here and for validating incoming tokens.</summary>
    public static SymmetricSecurityKey SigningKey(JwtOptions settings) =>
        new(Encoding.UTF8.GetBytes(settings.SigningKey));
}
