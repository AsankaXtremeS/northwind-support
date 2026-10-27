namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Who is making the current request, as proven by their token. Implemented by the host.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The signed-in agent's id.</summary>
    /// <exception cref="Exceptions.NotAuthenticatedException">The request carries no valid identity.</exception>
    int AgentId { get; }
}
