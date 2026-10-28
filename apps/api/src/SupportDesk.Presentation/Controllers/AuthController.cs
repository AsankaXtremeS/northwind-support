using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Application.Features.Auth.Me;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Signs an agent in and returns a token, its expiry and who they are. Any failure is the
    /// same 401 with the same message.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginResponse> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>The signed-in agent, or 401 without a valid token or once they are deactivated.</summary>
    [HttpGet("me")]
    public Task<AuthUserDto> Me([FromServices] GetCurrentAgentQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(ct);
}
