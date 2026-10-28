using System.Text;
using Microsoft.Extensions.Options;

namespace SupportDesk.Application.Auth;

/// <summary>
/// Rejects token settings that would be insecure or unusable. Runs at startup, so a missing
/// signing key stops the application instead of failing at the first sign-in.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    /// <summary>A day: long enough for any shift, short enough that a leaked token does not live on.</summary>
    public const int MaximumLifetimeMinutes = 24 * 60;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add($"{JwtOptions.SectionName}:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"{JwtOptions.SectionName}:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add(
                $"{JwtOptions.SectionName}:SigningKey is required. Set it with dotnet user-secrets " +
                "locally or with the Jwt__SigningKey environment variable. See README.md.");
        }
        else if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinimumSigningKeyBytes)
        {
            failures.Add($"{JwtOptions.SectionName}:SigningKey must be at least {JwtOptions.MinimumSigningKeyBytes} bytes.");
        }

        if (options.LifetimeMinutes is <= 0 or > MaximumLifetimeMinutes)
        {
            failures.Add($"{JwtOptions.SectionName}:LifetimeMinutes must be between 1 and {MaximumLifetimeMinutes}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
