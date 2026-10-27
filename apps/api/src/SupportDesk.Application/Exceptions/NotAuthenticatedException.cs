namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when a request needs a signed-in, active agent and does not have one, for example a
/// token for an agent who has since been deactivated. Surfaces as HTTP 401.
/// </summary>
public sealed class NotAuthenticatedException : Exception
{
    public const string GenericMessage = "Sign in to continue.";

    public NotAuthenticatedException() : base(GenericMessage)
    {
    }
}
