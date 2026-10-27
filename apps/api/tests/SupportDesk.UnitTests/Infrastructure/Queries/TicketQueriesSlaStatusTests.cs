using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Sla;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using Xunit.Abstractions;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The SLA rule translated to SQL and run against SQLite, through every query that reports a
/// status. Same cases as <see cref="Domain.Aggregates.Tickets.SlaStatusRuleTests"/>.
/// </summary>
public sealed class TicketQueriesSlaStatusTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private SupportDbContext _db = null!;
    private TicketQueries _queries = null!;
    private int _customerId;
    private int _categoryId;

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
        _db.AddRange(customer, category);
        await _db.SaveChangesAsync();
        (_customerId, _categoryId) = (customer.Id, category.Id);

        _queries = new TicketQueries(
            _db,
            new FixedClock(),
            Options.Create(new SlaOptions { AtRiskThresholdPercent = SlaStatusCases.AtRiskPercent }));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [MemberData(nameof(SlaStatusCases.All), MemberType = typeof(SlaStatusCases))]
    public async Task Queries_AtEachBoundary_ReportTheExpectedStatus(
        string scenario, int? dueOffsetSeconds, int? windowMinutes, int? resolvedOffsetSeconds, SlaStatus expected)
    {
        var ticket = SlaStatusCases.Ticket(dueOffsetSeconds, windowMinutes, resolvedOffsetSeconds, _customerId, _categoryId);
        _db.Add(ticket);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var detail = await _queries.GetDetailAsync(ticket.Id, CancellationToken.None);
        var listed = await _queries.GetPagedAsync(new TicketQuery().Normalized(), CancellationToken.None);
        var forCustomer = await _queries.GetForCustomerAsync(_customerId, CancellationToken.None);

        Assert.True(expected == detail!.SlaStatus, $"{scenario}: detail reported {detail.SlaStatus}, expected {expected}.");
        Assert.True(expected == listed.Items.Single().SlaStatus, $"{scenario}: list reported {listed.Items.Single().SlaStatus}, expected {expected}.");
        Assert.True(expected == forCustomer.Single().SlaStatus, $"{scenario}: customer tickets reported {forCustomer.Single().SlaStatus}, expected {expected}.");
    }

    [Theory]
    [InlineData(SlaStatus.NotApplicable)]
    [InlineData(SlaStatus.WithinSla)]
    [InlineData(SlaStatus.AtRisk)]
    [InlineData(SlaStatus.Breached)]
    [InlineData(SlaStatus.Met)]
    public async Task GetPagedAsync_WithAnSlaStatus_ReturnsOnlyTicketsInThatStatus(SlaStatus wanted)
    {
        var expected = await SeedEveryCaseAsync(wanted);

        var result = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = wanted }.Normalized(), CancellationToken.None);

        Assert.Equal(expected.Order(), result.Items.Select(item => item.Reference).Order());
        Assert.Equal(expected.Count, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(wanted, item.SlaStatus));
    }

    [Fact]
    public async Task GetPagedAsync_WithAnSlaStatusAndAnotherFilter_CombinesThemWithAnd()
    {
        await SeedEveryCaseAsync(SlaStatus.Breached);

        // Breached covers an open ticket past due and a late resolution; only the late one is Resolved.
        var result = await _queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.Breached, Status = TicketStatus.Resolved }.Normalized(),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(TicketStatus.Resolved, item.Status);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_WithAnSlaStatus_CountsAndPagesOnlyTheFilteredSet()
    {
        var breached = await SeedEveryCaseAsync(SlaStatus.Breached);

        var firstPage = await _queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.Breached, PageSize = 1 }.Normalized(), CancellationToken.None);

        Assert.Equal(2, breached.Count);
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Single(firstPage.Items);
    }

    /// <summary>One ticket per boundary case; returns the references expected to be in <paramref name="status"/>.</summary>
    private async Task<List<string>> SeedEveryCaseAsync(SlaStatus status)
    {
        var expected = new List<string>();
        var number = 0;

        foreach (var row in SlaStatusCases.All)
        {
            var reference = $"TCK-{++number:D4}";
            var (dueOffset, window, resolvedOffset, rowStatus) =
                ((int?)row[1], (int?)row[2], (int?)row[3], (SlaStatus)row[4]);

            _db.Add(SlaStatusCases.Ticket(dueOffset, window, resolvedOffset, _customerId, _categoryId, reference));

            if (rowStatus == status)
            {
                expected.Add(reference);
            }
        }

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        return expected;
    }
}
