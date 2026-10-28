namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Turns a password into a salted, one-way hash, and checks a password against one.
/// </summary>
/// <remarks>
/// An abstraction so the application never depends on the hashing library, and so the hash
/// format (salt, iteration count, algorithm) is owned by a vetted implementation, not by us.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>A new hash with its own random salt; hashing the same password twice gives different results.</summary>
    string Hash(string password);

    /// <summary>True when the password matches the hash. A malformed hash never matches.</summary>
    bool Verify(string passwordHash, string password);
}
