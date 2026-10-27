using System.Globalization;
using Microsoft.IdentityModel.JsonWebTokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Presentation.Auth;

/// <summary>Reads the agent id from the validated token's <c>sub</c> claim.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int AgentId
    {
        get
        {
            var subject = accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var agentId)
                ? agentId
                : throw new NotAuthenticatedException();
        }
    }
}
