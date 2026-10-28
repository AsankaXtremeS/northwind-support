using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.IntegrationTests.Auth;

/// <summary>POST /api/auth/login and GET /api/auth/me through the real pipeline.</summary>
public sealed class AuthEndpointsTests(SupportDeskApiFactory api) : IClassFixture<SupportDeskApiFactory>
{
    [Fact]
    public async Task Login_RightPassword_ReturnsATokenThatExpiresInEightHoursAndWhoSignedIn()
    {
        var response = await api.LoginAsync(SupportDeskApiFactory.SaraEmail);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json.Options);
        Assert.False(string.IsNullOrWhiteSpace(login!.Token));
        Assert.Equal("Sara Lindqvist", login.User.FullName);
        Assert.Equal(SupportDeskApiFactory.SaraEmail, login.User.Email);
        Assert.InRange(login.ExpiresAtUtc, DateTime.UtcNow.AddHours(8).AddMinutes(-1), DateTime.UtcNow.AddHours(8).AddMinutes(1));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401AndNoToken()
    {
        var response = await api.LoginAsync(SupportDeskApiFactory.SaraEmail, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_ReturnTheSame401()
    {
        var unknownEmail = await api.LoginAsync("nobody@northwind-support.example");
        var wrongPassword = await api.LoginAsync(SupportDeskApiFactory.SaraEmail, "wrong-password");

        var unknownProblem = await unknownEmail.Content.ReadFromJsonAsync<ProblemDetails>(Json.Options);
        var wrongProblem = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>(Json.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongProblem!.Title, unknownProblem!.Title);
        Assert.Equal(wrongProblem.Detail, unknownProblem.Detail);
        Assert.Equal("Email or password is incorrect.", unknownProblem.Detail);
    }

    [Fact]
    public async Task Login_InactiveAgentWithTheRightPassword_Returns401()
    {
        var response = await api.LoginAsync(SupportDeskApiFactory.YukiEmail);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyFields_Returns400()
    {
        var response = await api.LoginAsync("", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithSarasToken_ReturnsSara()
    {
        var client = await api.SignedInClientAsync();

        var me = await client.GetFromJsonAsync<AuthUserDto>("/api/auth/me", Json.Options);

        Assert.Equal("Sara Lindqvist", me!.FullName);
        Assert.Equal(SupportDeskApiFactory.SaraEmail, me.Email);
    }

    [Fact]
    public async Task Me_AgentDeactivatedAfterSigningIn_Returns401()
    {
        // Its own API and database, so deactivating Sara does not affect the other tests.
        await using var isolated = new SupportDeskApiFactory();
        await ((IAsyncLifetime)isolated).InitializeAsync();
        var client = await isolated.SignedInClientAsync();

        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var sara = await db.Set<Agent>().SingleAsync(a => a.Email == SupportDeskApiFactory.SaraEmail);
            sara.Deactivate();
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
