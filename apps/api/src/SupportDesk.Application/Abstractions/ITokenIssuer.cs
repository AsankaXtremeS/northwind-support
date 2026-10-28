using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Issues the signed token a client sends back on every request. Implemented by the host,
/// which owns the token format and its validation.
/// </summary>
public interface ITokenIssuer
{
    IssuedToken Issue(AuthUserDto user);
}

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);
