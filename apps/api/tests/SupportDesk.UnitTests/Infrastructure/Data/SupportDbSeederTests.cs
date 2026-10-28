using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

/// <summary>
/// The development seeder against an in-memory SQLite database: agents get a hashed password,
/// including on a database that was seeded before logins existed.
/// </summary>
public sealed class SupportDbSeederTests : IAsyncLifetime
{
    private const string AgentPassword = "unit-tests-only-password";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IdentityPasswordHasher _hasher = new();
    private SupportDbContext _db = null!;

    public async Task InitializeAsync()
    {
        // The in-memory database lives only as long as this connection stays open.
        await _connection.OpenAsync();

        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private SupportDbSeeder Seeder(string agentPassword = AgentPassword) =>
        new(
            _db,
            new FixedClock(new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc)),
            _hasher,
            Options.Create(new SeedOptions { AgentPassword = agentPassword }),
            NullLogger<SupportDbSeeder>.Instance);

    [Fact]
    public async Task SeedAsync_EmptyDatabase_GivesEveryAgentAHashOfTheConfiguredPassword()
    {
        await Seeder().SeedAsync();

        var agents = await _db.Set<Agent>().AsNoTracking().ToListAsync();

        Assert.Equal(6, agents.Count);
        Assert.All(agents, agent =>
        {
            Assert.NotNull(agent.PasswordHash);
            Assert.NotEqual(AgentPassword, agent.PasswordHash);
            Assert.True(_hasher.Verify(agent.PasswordHash, AgentPassword));
        });

        // Each agent has its own salt.
        Assert.Equal(agents.Count, agents.Select(a => a.PasswordHash).Distinct().Count());
    }

    [Fact]
    public async Task SeedAsync_SeededBeforeLoginsExisted_AddsPasswordsWithoutReseeding()
    {
        await Seeder().SeedAsync();
        var ticketCount = await _db.Set<Ticket>().CountAsync();
        await _db.Database.ExecuteSqlRawAsync("UPDATE Agents SET PasswordHash = NULL");
        _db.ChangeTracker.Clear();

        await Seeder().SeedAsync();

        var agents = await _db.Set<Agent>().AsNoTracking().ToListAsync();
        Assert.Equal(6, agents.Count);
        Assert.Equal(ticketCount, await _db.Set<Ticket>().CountAsync());
        Assert.All(agents, agent => Assert.True(_hasher.Verify(agent.PasswordHash!, AgentPassword)));
    }

    [Fact]
    public async Task SeedAsync_AgentAlreadyHasAPassword_KeepsIt()
    {
        await Seeder().SeedAsync();
        var sara = await _db.Set<Agent>().SingleAsync(a => a.Email == "sara.lindqvist@northwind-support.example");
        var ownHash = _hasher.Hash("Sara's own password");
        sara.SetPasswordHash(ownHash);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await Seeder().SeedAsync();

        var reloaded = await _db.Set<Agent>().AsNoTracking().SingleAsync(a => a.Id == sara.Id);
        Assert.Equal(ownHash, reloaded.PasswordHash);
    }

    [Fact]
    public async Task SeedAsync_InactiveAgent_StillGetsAPassword()
    {
        await Seeder().SeedAsync();

        var yuki = await _db.Set<Agent>().AsNoTracking().SingleAsync(a => a.Email == "yuki.tanaka@northwind-support.example");

        Assert.False(yuki.IsActive);
        Assert.NotNull(yuki.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SeedAsync_NoPasswordConfigured_SeedsAgentsWithoutALogin(string agentPassword)
    {
        await Seeder(agentPassword).SeedAsync();

        var agents = await _db.Set<Agent>().AsNoTracking().ToListAsync();

        Assert.Equal(6, agents.Count);
        Assert.All(agents, agent => Assert.Null(agent.PasswordHash));
    }
}
