using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.IntegrationTests;

/// <summary>
/// The whole API in memory, on a seeded in-memory SQLite database instead of SQL Server.
/// The environment is "Testing", so the development-only startup (SQL Server migrations,
/// Swagger) does not run; the database is created and seeded here instead.
/// </summary>
public sealed class SupportDeskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Test-only key, also used by tests that forge tokens.</summary>
    public const string SigningKey = "integration-tests-only-signing-key-not-a-secret";

    public const string SaraEmail = "sara.lindqvist@northwind-support.example";
    public const string YukiEmail = "yuki.tanaka@northwind-support.example";

    // The in-memory database lives only as long as this connection stays open.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Read before the services below replace the database, so it only has to exist.
        builder.UseSetting("ConnectionStrings:Default", "unused - replaced by SQLite");
        builder.UseSetting("Jwt:SigningKey", SigningKey);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>Signs in through the real endpoint, with the development password.</summary>
    public Task<HttpResponseMessage> LoginAsync(string email, string password = SupportDbSeeder.DevelopmentPassword) =>
        CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    /// <summary>A client carrying Sara's token, obtained by actually signing in.</summary>
    public async Task<HttpClient> SignedInClientAsync(string email = SaraEmail)
    {
        var response = await LoginAsync(email);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json.Options);

        return ClientWithToken(login!.Token);
    }

    public HttpClient ClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
