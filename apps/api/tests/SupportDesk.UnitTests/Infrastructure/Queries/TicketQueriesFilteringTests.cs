using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Sla;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using Xunit.Abstractions;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The ticket list filters, run as real SQL against an in-memory SQLite database. Search terms
/// use the same case as the data, so these test the filter logic rather than the collation.
/// </summary>
public sealed class TicketQueriesFilteringTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private SupportDbContext _db = null!;
    private TicketQueries _queries = null!;

    private int _contosoId;
    private int _fabrikamId;
    private int _billingId;
    private int _technicalId;
    private int _saraId;

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
        await SeedAsync();

        _queries = new TicketQueries(_db, new FixedClock(), Options.Create(new SlaOptions { AtRiskThresholdPercent = 25 }));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetPagedAsync_WithPriority_ReturnsOnlyExactMatches()
    {
        var result = await GetPagedAsync(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(["TCK-0001", "TCK-0003"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_WithStatusAndCategory_CombinesThemWithAnd()
    {
        var result = await GetPagedAsync(new TicketQuery { Status = TicketStatus.Open, CategoryId = _billingId });

        Assert.Equal(["TCK-0002", "TCK-0003"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_WithCustomerAndAgent_ReturnsOnlyExactMatches()
    {
        var result = await GetPagedAsync(new TicketQuery { CustomerId = _fabrikamId, AssignedAgentId = _saraId });

        Assert.Equal(["TCK-0002", "TCK-0004"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_WithSearch_MatchesTheCustomerName()
    {
        var result = await GetPagedAsync(new TicketQuery { Search = "Fabrikam" });

        Assert.Equal(["TCK-0002", "TCK-0004", "TCK-0005"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_WithSearch_MatchesTheTitle()
    {
        var result = await GetPagedAsync(new TicketQuery { Search = "Invoice" });

        Assert.Equal(["TCK-0002", "TCK-0003"], References(result));
    }

    [Theory]
    [InlineData("TCK-0004")]
    [InlineData("0004")]
    public async Task GetPagedAsync_WithSearch_MatchesAFullOrPartialReference(string search)
    {
        var result = await GetPagedAsync(new TicketQuery { Search = search });

        Assert.Equal(["TCK-0004"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_WithPaddedSearch_TrimsIt()
    {
        var result = await GetPagedAsync(new TicketQuery { Search = "  Invoice  " });

        Assert.Equal(["TCK-0002", "TCK-0003"], References(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPagedAsync_WithBlankSearch_IgnoresIt(string? search)
    {
        var result = await GetPagedAsync(new TicketQuery { Search = search });

        Assert.Equal(6, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_WithUnassignedOnly_ReturnsOnlyTicketsWithoutAnAgent()
    {
        var result = await GetPagedAsync(new TicketQuery { UnassignedOnly = true });

        Assert.Equal(["TCK-0001", "TCK-0003", "TCK-0005", "TCK-0006"], References(result));
        Assert.All(result.Items, item => Assert.Null(item.AssignedAgent));
    }

    [Fact]
    public async Task GetPagedAsync_WithUnassignedOnlyAndAnAgent_ReturnsAnEmptyPage()
    {
        var result = await GetPagedAsync(new TicketQuery { UnassignedOnly = true, AssignedAgentId = _saraId });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_WithAFilter_CountsAndPagesOnlyTheFilteredSet()
    {
        var firstPage = await GetPagedAsync(new TicketQuery { Search = "Fabrikam", PageSize = 2 });
        var secondPage = await GetPagedAsync(new TicketQuery { Search = "Fabrikam", PageSize = 2, Page = 2 });

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
    }

    private Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query) =>
        _queries.GetPagedAsync(query.Normalized(), CancellationToken.None);

    private static IEnumerable<string> References(PagedResult<TicketListItemDto> result) =>
        result.Items.Select(item => item.Reference).Order();

    /// <summary>
    /// Six tickets across two customers, two categories and one agent, chosen so that each
    /// filter has something to keep and something to drop.
    /// </summary>
    private async Task SeedAsync()
    {
        var contoso = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, FixedClock.DefaultNow);
        var fabrikam = new Customer("Fabrikam Inc", "help@fabrikam.example", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var billing = new Category("Billing", requiresSpecialist: false, forcesCriticalPriority: false);
        var technical = new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
        var sara = new Agent("Sara Lindqvist", "sara@northwind.example", maxOpenTickets: 10, FixedClock.DefaultNow);

        _db.AddRange(contoso, fabrikam, billing, technical, sara);
        await _db.SaveChangesAsync();

        (_contosoId, _fabrikamId) = (contoso.Id, fabrikam.Id);
        (_billingId, _technicalId) = (billing.Id, technical.Id);
        _saraId = sara.Id;

        _db.AddRange(
            Ticket("TCK-0001", "Cannot log in to the portal", _contosoId, _technicalId, TicketPriority.High, TicketStatus.New),
            Ticket("TCK-0002", "Invoice total is wrong", _fabrikamId, _billingId, TicketPriority.Critical, TicketStatus.Open, _saraId),
            Ticket("TCK-0003", "Invoice is missing the VAT number", _contosoId, _billingId, TicketPriority.High, TicketStatus.Open),
            Ticket("TCK-0004", "Export produces an empty file", _fabrikamId, _technicalId, TicketPriority.Low, TicketStatus.InProgress, _saraId),
            Ticket("TCK-0005", "Sync job fails overnight", _fabrikamId, _technicalId, TicketPriority.Medium, TicketStatus.Open),
            Ticket("TCK-0006", "Refund not received", _contosoId, _billingId, TicketPriority.Low, TicketStatus.Resolved));
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
    }

    private static Ticket Ticket(
        string reference,
        string title,
        int customerId,
        int categoryId,
        TicketPriority priority,
        TicketStatus status,
        int? agentId = null) =>
        new TicketBuilder()
            .WithReference(reference)
            .WithTitle(title)
            .ForCustomer(customerId)
            .InCategory(categoryId)
            .WithPriority(priority)
            .WithStatus(status)
            .AssignedTo(agentId)
            .Build();
}
