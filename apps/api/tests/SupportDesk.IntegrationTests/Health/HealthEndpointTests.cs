using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.IntegrationTests.Health;

/// <summary>
/// /health is open without a token, reports the database check, and says 503 - without any
/// exception or connection detail - when the database cannot be reached.
/// </summary>
public sealed class HealthEndpointTests(SupportDeskApiFactory api) : IClassFixture<SupportDeskApiFactory>
{
    private sealed record HealthBody(string Status, Dictionary<string, string> Checks);

    [Fact]
    public async Task Health_WithoutAToken_Returns200AndAHealthyDatabase()
    {
        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<HealthBody>(Json.Options);
        Assert.Equal("Healthy", body!.Status);
        Assert.Equal("Healthy", body.Checks["database"]);
    }

    [Fact]
    public async Task Health_DatabaseUnreachable_Returns503WithoutExceptionDetail()
    {
        // A database file in a folder that does not exist, opened read-only: it can never connect.
        var missingFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.db");

        using var unreachable = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.AddDbContext<SupportDbContext>(options =>
                options.UseSqlite($"Data Source={missingFile};Mode=ReadOnly"));
        }));

        var response = await unreachable.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missing.db", text, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadFromJsonAsync<HealthBody>(Json.Options);
        Assert.Equal("Unhealthy", body!.Status);
        Assert.Equal("Unhealthy", body.Checks["database"]);
    }
}
