using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.IntegrationTests.Tickets;

/// <summary>AU-8: the escalation history records the signed-in agent, whatever the body says.</summary>
public sealed class EscalationActorTests(SupportDeskApiFactory api) : IClassFixture<SupportDeskApiFactory>
{
    [Fact]
    public async Task Escalate_BodyNamesSomeoneElse_RecordsTheSignedInAgent()
    {
        var client = await api.SignedInClientAsync(SupportDeskApiFactory.SaraEmail);
        var ticketId = await AnEscalatableTicketIdAsync();

        // An old client, or someone trying it on: the extra field is simply ignored.
        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/escalate",
            new { reason = "Customer is losing sales.", escalatedBy = "Somebody Else" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Sara Lindqvist", body.RootElement.GetProperty("escalation").GetProperty("escalatedBy").GetString());

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var saraId = await db.Set<Agent>().Where(a => a.Email == SupportDeskApiFactory.SaraEmail).Select(a => a.Id).SingleAsync();
        var recorded = await db.Set<TicketEscalation>().AsNoTracking().SingleAsync(e => e.TicketId == ticketId);

        Assert.Equal(saraId, recorded.EscalatedByAgentId);
        Assert.Equal("Sara Lindqvist", recorded.EscalatedBy);
    }

    /// <summary>A seeded ticket that is still open and below Critical.</summary>
    private async Task<int> AnEscalatableTicketIdAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();

        return await db.Set<Ticket>()
            .Where(t => t.Priority != TicketPriority.Critical
                && t.Status != TicketStatus.Resolved
                && t.Status != TicketStatus.Closed)
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .FirstAsync();
    }
}
