using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// ASP.NET Core Identity's password hasher: PBKDF2 with HMAC-SHA512, a random 128-bit salt and
/// 100,000 iterations, stored with a version marker so the format can be upgraded later.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    // Identity's hasher takes a user, but the default implementation never reads it.
    private static readonly object NoUser = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        return _hasher.HashPassword(NoUser, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            // SuccessRehashNeeded still means the password is right.
            return _hasher.VerifyHashedPassword(NoUser, passwordHash, password)
                is not PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Not a hash this hasher produced (e.g. not Base64): treat it as a mismatch.
            return false;
        }
    }
}
