using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Me;

/// <summary>
/// The signed-in agent, read fresh from the database rather than from the token, so a renamed
/// agent shows their new name and a deactivated agent is turned away.
/// </summary>
public sealed class GetCurrentAgentQueryHandler(ICurrentUser currentUser, IAgentRepository agents)
{
    /// <exception cref="NotAuthenticatedException">The agent no longer exists or is inactive.</exception>
    public async Task<AuthUserDto> HandleAsync(CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(currentUser.AgentId, ct);

        if (agent is not { IsActive: true })
        {
            throw new NotAuthenticatedException();
        }

        return new AuthUserDto(agent.Id, agent.FullName, agent.Email);
    }
}
