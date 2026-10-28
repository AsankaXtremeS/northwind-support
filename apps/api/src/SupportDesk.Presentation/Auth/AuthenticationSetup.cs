using System.Net.Mime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Auth;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Presentation.Auth;

public static class AuthenticationSetup
{
    /// <summary>
    /// JWT bearer sign-in, with every endpoint closed unless it says <c>[AllowAnonymous]</c>.
    /// </summary>
    /// <remarks>
    /// The fallback policy is what closes everything: it applies to any endpoint with no
    /// authorization metadata of its own, so a new controller is protected without anyone
    /// remembering to add <c>[Authorize]</c>. Swagger is middleware, not an endpoint, so it stays open.
    /// </remarks>
    public static IServiceCollection AddTokenAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // A missing or short signing key stops the application here, not at the first sign-in.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Validation reads the same JwtOptions the issuer signs with, so the two cannot drift.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                // Keep claim names as issued ("sub", "name") instead of mapping them to long URIs.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = JwtTokenIssuer.SigningKey(settings),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    // Small allowance for clock drift; the default of 5 minutes outlives the token noticeably.
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                bearer.Events = new JwtBearerEvents { OnChallenge = WriteUnauthorizedProblemAsync };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>A 401 in the same problem-document shape as every other error the API returns.</summary>
    private static async Task WriteUnauthorizedProblemAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();

        var problem = new ProblemDetails
        {
            Title = "Unauthorized",
            Detail = NotAuthenticatedException.GenericMessage,
            Status = StatusCodes.Status401Unauthorized
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;

        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, MediaTypeNames.Application.Json);
    }
}
