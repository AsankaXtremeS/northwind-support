using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public sealed class AgentTests
{
    private static Agent NewAgent() =>
        new("Sara Lindqvist", "sara.lindqvist@northwind-support.example", 6, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Constructor_NewAgent_HasNoPasswordHash()
    {
        Assert.Null(NewAgent().PasswordHash);
    }

    [Fact]
    public void SetPasswordHash_ValidHash_StoresIt()
    {
        var agent = NewAgent();

        agent.SetPasswordHash("AQAAAAIAAYagAAAAEhash");

        Assert.Equal("AQAAAAIAAYagAAAAEhash", agent.PasswordHash);
    }

    [Fact]
    public void SetPasswordHash_CalledAgain_ReplacesTheHash()
    {
        var agent = NewAgent();
        agent.SetPasswordHash("first-hash");

        agent.SetPasswordHash("second-hash");

        Assert.Equal("second-hash", agent.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_BlankHash_Throws(string hash)
    {
        Assert.Throws<ArgumentException>(() => NewAgent().SetPasswordHash(hash));
    }

    [Fact]
    public void SetPasswordHash_LongerThanTheColumn_Throws()
    {
        var tooLong = new string('a', Agent.PasswordHashMaxLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => NewAgent().SetPasswordHash(tooLong));
    }
}
