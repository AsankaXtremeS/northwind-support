using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Application.Sla;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;
using Xunit.Abstractions;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Escalations saved through the ticket aggregate and read back as history, as real SQL against
/// an in-memory SQLite database.
/// </summary>
public sealed class TicketEscalationHistoryTests(ITestOutputHelper output) : IAsyncLifetime
{
    private static readonly DateTime FirstEscalation = FixedClock.DefaultNow.AddHours(1);
    private static readonly DateTime SecondEscalation = FixedClock.DefaultNow.AddHours(2);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private SupportDbContext _db = null!;
    private TicketQueries _queries = null!;
    private Agent _alex = null!;
    private Agent _ben = null!;
    private int _ticketId;

    public async Task InitializeAsync()
    {
        // The in-memory database lives only as long as this connection stays open.
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .LogTo(output.WriteLine, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .Options;

        _db = new SupportDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, FixedClock.DefaultNow);
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _alex = new Agent("Alex Turner", "alex@example.com", maxOpenTickets: 10, FixedClock.DefaultNow);
        _ben = new Agent("Ben Osei", "ben@example.com", maxOpenTickets: 8, FixedClock.DefaultNow);
        _db.AddRange(customer, category, _alex, _ben);
        await _db.SaveChangesAsync();

        var ticket = new TicketBuilder()
            .ForCustomer(customer.Id)
            .InCategory(category.Id)
            .WithPriority(TicketPriority.Medium)
            .AssignedTo(_alex.Id)
            .WithSla(FixedClock.DefaultNow.AddHours(24), windowMinutes: 1440)
            .Build();
        _db.Add(ticket);
        await _db.SaveChangesAsync();
        _ticketId = ticket.Id;
        _db.ChangeTracker.Clear();

        _queries = new TicketQueries(_db, new FixedClock(), Options.Create(new SlaOptions { AtRiskThresholdPercent = 25 }));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetEscalationsAsync_AfterTwoEscalations_ReturnsBothNewestFirstWithAgentNames()
    {
        await EscalateAsync(toAgentId: _ben.Id, windowMinutes: 480, "Customer is losing sales.", _alex, FirstEscalation);
        await EscalateAsync(toAgentId: null, windowMinutes: 240, "Outage spreading to other sites.", _ben, SecondEscalation);

        var history = await _queries.GetEscalationsAsync(_ticketId, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Collection(
            history,
            newest =>
            {
                Assert.Equal(TicketPriority.High, newest.FromPriority);
                Assert.Equal(TicketPriority.Critical, newest.ToPriority);
                Assert.Equal(new AgentSummaryDto(_ben.Id, "Ben Osei"), newest.FromAgent);
                Assert.Null(newest.ToAgent);
                Assert.Equal(FirstEscalation.AddMinutes(480), newest.FromDueAtUtc);
                Assert.Equal(SecondEscalation.AddMinutes(240), newest.ToDueAtUtc);
                Assert.Equal("Ben Osei", newest.EscalatedBy);
                Assert.Equal(SecondEscalation, newest.EscalatedAtUtc);
            },
            oldest =>
            {
                Assert.Equal(TicketPriority.Medium, oldest.FromPriority);
                Assert.Equal(TicketPriority.High, oldest.ToPriority);
                Assert.Equal(new AgentSummaryDto(_alex.Id, "Alex Turner"), oldest.FromAgent);
                Assert.Equal(new AgentSummaryDto(_ben.Id, "Ben Osei"), oldest.ToAgent);
                Assert.Equal(FixedClock.DefaultNow.AddHours(24), oldest.FromDueAtUtc);
                Assert.Equal("Customer is losing sales.", oldest.Reason);
                Assert.Equal("Alex Turner", oldest.EscalatedBy);
            });
    }

    [Fact]
    public async Task GetEscalationsAsync_ForATicketNeverEscalated_ReturnsAnEmptyList()
    {
        var history = await _queries.GetEscalationsAsync(_ticketId, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetEscalationsAsync_ForAnUnknownTicket_ReturnsNull()
    {
        Assert.Null(await _queries.GetEscalationsAsync(_ticketId + 1000, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ForAnUnknownTicket_ThrowsNotFound()
    {
        var handler = new GetTicketEscalationsQueryHandler(_queries);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(_ticketId + 1000, CancellationToken.None));
    }

    [Fact]
    public async Task GetDetailAsync_ReportsCanEscalateUntilTheTicketIsCritical()
    {
        Assert.True((await _queries.GetDetailAsync(_ticketId, CancellationToken.None))!.CanEscalate);

        await EscalateAsync(toAgentId: _alex.Id, windowMinutes: 480, "Customer is losing sales.", _alex, FirstEscalation);
        Assert.True((await _queries.GetDetailAsync(_ticketId, CancellationToken.None))!.CanEscalate);

        await EscalateAsync(toAgentId: _alex.Id, windowMinutes: 240, "Outage spreading to other sites.", _alex, SecondEscalation);
        Assert.False((await _queries.GetDetailAsync(_ticketId, CancellationToken.None))!.CanEscalate);
    }

    /// <summary>Escalates the way the command does: load tracked, escalate, save the unit of work.</summary>
    private async Task EscalateAsync(int? toAgentId, int windowMinutes, string reason, Agent escalatedBy, DateTime now)
    {
        var ticket = (await new TicketRepository(_db).GetByIdAsync(_ticketId, CancellationToken.None))!;
        var triage = new TriageDecision(
            new PriorityDecision(ticket.NextPriority(), "Escalated."),
            new SlaWindow(windowMinutes, "Window."),
            new AssignmentDecision(toAgentId, null, "Assignment."));

        ticket.Escalate(triage, reason, new EscalationActor(escalatedBy.Id, escalatedBy.FullName), now);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }
}
