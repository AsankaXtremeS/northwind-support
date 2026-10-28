using System.Net.Mime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Extensions;

/// <summary>
/// <c>GET /health</c>: whether the API can reach its database. Open without a token, so a
/// container orchestrator or load balancer can probe it.
/// </summary>
public static class HealthCheckExtensions
{
    public const string Path = "/health";

    /// <summary>Registers the checks: one, the database, via EF Core's CanConnectAsync.</summary>
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<SupportDbContext>("database");

        return services;
    }

    /// <summary>
    /// Maps the endpoint. The fallback policy closes every endpoint, so this is the one place
    /// that opens it again. 200 when healthy, 503 when not; the body names each check and its
    /// status, never an exception or connection detail.
    /// </summary>
    public static IEndpointConventionBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapHealthChecks(Path, new HealthCheckOptions { ResponseWriter = WriteJsonAsync })
            .AllowAnonymous();

    private static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;

        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        });
    }
}
