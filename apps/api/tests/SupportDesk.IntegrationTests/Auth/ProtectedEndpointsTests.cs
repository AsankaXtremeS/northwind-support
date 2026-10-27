using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace SupportDesk.IntegrationTests.Auth;

/// <summary>
/// Every endpoint, existing and new, is closed without a valid token. Nothing on the controllers
/// says so: the fallback policy does it.
/// </summary>
public sealed class ProtectedEndpointsTests(SupportDeskApiFactory api) : IClassFixture<SupportDeskApiFactory>
{
    public static TheoryData<string, string> AllEndpoints => new()
    {
        { "GET", "/api/tickets" },
        { "GET", "/api/tickets/1" },
        { "POST", "/api/tickets" },
        { "PATCH", "/api/tickets/1/status" },
        { "PATCH", "/api/tickets/1/assignment" },
        { "POST", "/api/tickets/1/escalate" },
        { "GET", "/api/tickets/1/escalations" },
        { "GET", "/api/customers" },
        { "GET", "/api/customers/1" },
        { "GET", "/api/agents" },
        { "GET", "/api/categories" },
        { "GET", "/api/auth/me" }
    };

    public static TheoryData<string> ReadEndpoints => new()
    {
        "/api/tickets",
        "/api/tickets/1",
        "/api/tickets/1/escalations",
        "/api/tickets?slaStatus=Breached",
        "/api/customers",
        "/api/customers/1",
        "/api/agents",
        "/api/categories",
        "/api/auth/me"
    };

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Request_WithoutAToken_Returns401ProblemDocument(string method, string url)
    {
        var response = await api.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = method == "GET" ? null : JsonContent.Create(new { })
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json.Options);
        Assert.Equal(401, problem!.Status);
        Assert.Equal("Sign in to continue.", problem.Detail);
    }

    [Theory]
    [MemberData(nameof(ReadEndpoints))]
    public async Task Request_WithAValidToken_Returns200(string url)
    {
        var client = await api.SignedInClientAsync();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithATokenSignedWithAnotherKey_Returns401()
    {
        var response = await api.ClientWithToken(ForgedTokens.SignedWithAnotherKey()).GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithAnExpiredToken_Returns401()
    {
        var response = await api.ClientWithToken(ForgedTokens.Expired()).GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithAGarbageToken_Returns401()
    {
        var response = await api.ClientWithToken("not-a-jwt").GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithAValidTokenButAnInvalidBody_GetsPastSignInTo400()
    {
        var client = await api.SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/tickets/1/escalate", new { reason = "abc" });

        // Proves the token was accepted: the request reached validation.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
