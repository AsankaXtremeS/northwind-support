namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when sign-in fails, for any reason. Surfaces as HTTP 401 with one fixed message, so a
/// caller cannot tell an unknown email from a wrong password or an inactive agent.
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public const string GenericMessage = "Email or password is incorrect.";

    public InvalidCredentialsException() : base(GenericMessage)
    {
    }
}
