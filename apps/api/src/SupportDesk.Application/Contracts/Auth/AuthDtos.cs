namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in agent: enough for the UI to greet them.</summary>
public sealed record AuthUserDto(int Id, string FullName, string Email);

/// <summary>Response of POST /api/auth/login.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, AuthUserDto User);
