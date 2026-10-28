using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;
using Xunit.Abstractions;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

/// <summary>
/// Loading assignment candidates, as real SQL against an in-memory SQLite database.
/// </summary>
public sealed class AgentRepositoryTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private SupportDbContext _db = null!;
    private AgentRepository _agents = null!;
    private int _commandsExecuted;
    private int _lastReference;

    private int _customerId;
    private Category _general = null!;
    private Category _billing = null!;
    private Agent _alex = null!;
    private Agent _sara = null!;
    private Agent _yuki = null!;

    public async Task InitializeAsync()
    {
        // The in-memory database lives only as long as this connection stays open.
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .LogTo(message =>
                {
                    _commandsExecuted++;
                    output.WriteLine(message);
                },
                [RelationalEventId.CommandExecuted],
                LogLevel.Information)
            .Options;

        _db = new SupportDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await SeedAsync();

        _agents = new AgentRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetCandidatesAsync_ReturnsEveryAgentInOneQuery()
    {
        _commandsExecuted = 0;

        var candidates = await _agents.GetCandidatesAsync(_general.Id, excludeTicketId: null, CancellationToken.None);

        Assert.Equal([_alex.Id, _sara.Id, _yuki.Id], candidates.Select(c => c.Id));
        Assert.Equal(1, _commandsExecuted);
    }

    [Fact]
    public async Task GetCandidatesAsync_CountsOnlyOpenTicketsAssignedToEachAgent()
    {
        await AddTicketsAsync(_alex.Id, TicketStatus.New, TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed);
        await AddTicketsAsync(_sara.Id, TicketStatus.Open);
        await AddTicketsAsync(null, TicketStatus.Open);

        var candidates = await _agents.GetCandidatesAsync(_general.Id, excludeTicketId: null, CancellationToken.None);

        Assert.Equal(3, Candidate(candidates, _alex).OpenTicketCount);
        Assert.Equal(1, Candidate(candidates, _sara).OpenTicketCount);
        Assert.Equal(0, Candidate(candidates, _yuki).OpenTicketCount);
    }

    [Fact]
    public async Task GetCandidatesAsync_WithATicketToExclude_DoesNotCountItAgainstItsOwner()
    {
        var tickets = await AddTicketsAsync(_alex.Id, TicketStatus.Open, TicketStatus.Open);

        var candidates = await _agents.GetCandidatesAsync(_general.Id, tickets[0].Id, CancellationToken.None);

        Assert.Equal(1, Candidate(candidates, _alex).OpenTicketCount);
    }

    [Fact]
    public async Task GetCandidatesAsync_ForACategoryWithoutSpecialistRule_LetsEveryAgentMeetIt()
    {
        var candidates = await _agents.GetCandidatesAsync(_general.Id, excludeTicketId: null, CancellationToken.None);

        Assert.All(candidates, c => Assert.True(c.MeetsSpecialistRequirement));
    }

    [Fact]
    public async Task GetCandidatesAsync_ForASpecialistCategory_OnlySpecialistsMeetIt()
    {
        var candidates = await _agents.GetCandidatesAsync(_billing.Id, excludeTicketId: null, CancellationToken.None);

        Assert.True(Candidate(candidates, _sara).MeetsSpecialistRequirement);
        Assert.False(Candidate(candidates, _alex).MeetsSpecialistRequirement);
        Assert.False(Candidate(candidates, _yuki).MeetsSpecialistRequirement);
    }

    [Fact]
    public async Task GetCandidatesAsync_IncludesInactiveAgentsFlaggedAsSuch()
    {
        var candidates = await _agents.GetCandidatesAsync(_general.Id, excludeTicketId: null, CancellationToken.None);

        var yuki = Candidate(candidates, _yuki);
        Assert.False(yuki.IsActive);
        Assert.Equal("Yuki Tanaka", yuki.FullName);
        Assert.Equal(8, yuki.MaxOpenTickets);
    }

    private static AgentCandidate Candidate(IEnumerable<AgentCandidate> candidates, Agent agent) =>
        candidates.Single(c => c.Id == agent.Id);

    private async Task<List<Ticket>> AddTicketsAsync(int? agentId, params TicketStatus[] statuses)
    {
        var tickets = statuses
            .Select(status => new TicketBuilder()
                .WithReference($"TCK-{++_lastReference:D4}")
                .ForCustomer(_customerId)
                .InCategory(_general.Id)
                .AssignedTo(agentId)
                .WithStatus(status)
                .Build())
            .ToList();

        _db.AddRange(tickets);
        await _db.SaveChangesAsync();

        return tickets;
    }

    private async Task SeedAsync()
    {
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, FixedClock.DefaultNow);
        _general = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _alex = new Agent("Alex Turner", "alex@example.com", maxOpenTickets: 10, FixedClock.DefaultNow);
        _sara = new Agent("Sara Lindqvist", "sara@example.com", maxOpenTickets: 6, FixedClock.DefaultNow);
        _yuki = new Agent("Yuki Tanaka", "yuki@example.com", maxOpenTickets: 8, FixedClock.DefaultNow);
        _yuki.Deactivate();

        _db.AddRange(customer, _general, _billing, _alex, _sara, _yuki);
        await _db.SaveChangesAsync();

        _sara.AddSpecialization(_billing.Id);
        await _db.SaveChangesAsync();

        _customerId = customer.Id;
    }
}
