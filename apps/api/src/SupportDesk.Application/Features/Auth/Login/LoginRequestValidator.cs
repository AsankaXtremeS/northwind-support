using FluentValidation;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Features.Auth.Login;

/// <summary>
/// Only checks that both fields are there. Whether they are right is the handler's job, and it
/// answers with one generic message.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>The Agents.Email column length.</summary>
    public const int EmailMaxLength = 256;

    /// <summary>Generous for a passphrase, and stops megabyte "passwords" reaching the hasher.</summary>
    public const int PasswordMaxLength = 512;

    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Enter your email address.")
            .MaximumLength(EmailMaxLength);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Enter your password.")
            .MaximumLength(PasswordMaxLength);
    }
}
