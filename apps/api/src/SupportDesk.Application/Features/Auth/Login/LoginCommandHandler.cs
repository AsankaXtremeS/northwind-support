using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Login;

/// <summary>
/// Signs an agent in with their email and password and issues a token.
/// </summary>
/// <remarks>
/// Every failure (unknown email, no password set, wrong password, inactive agent) throws the
/// same <see cref="InvalidCredentialsException"/>. An unknown email still costs one password
/// hash, so the response time does not reveal which emails exist either.
/// </remarks>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    ILogger<LoginCommandHandler> logger)
{
    /// <exception cref="InvalidCredentialsException">The agent cannot sign in with these credentials.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        if (agent?.PasswordHash is null)
        {
            // Same work as a real check, so timing does not give the answer away.
            passwordHasher.Hash(request.Password);

            logger.LogInformation("Sign-in rejected: no agent with a login for that email.");
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(agent.PasswordHash, request.Password))
        {
            logger.LogInformation("Sign-in rejected for agent {AgentId}: wrong password.", agent.Id);
            throw new InvalidCredentialsException();
        }

        if (!agent.IsActive)
        {
            logger.LogInformation("Sign-in rejected for agent {AgentId}: agent is inactive.", agent.Id);
            throw new InvalidCredentialsException();
        }

        var user = new AuthUserDto(agent.Id, agent.FullName, agent.Email);
        var token = tokenIssuer.Issue(user);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponse(token.Token, token.ExpiresAtUtc, user);
    }
}
