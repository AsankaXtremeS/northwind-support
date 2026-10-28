using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

/// <summary>
/// The rule compiled to C#. <see cref="Infrastructure.Queries.TicketQueriesSlaStatusTests"/>
/// runs the same cases as SQL.
/// </summary>
public class SlaStatusRuleTests
{
    [Theory]
    [MemberData(nameof(SlaStatusCases.All), MemberType = typeof(SlaStatusCases))]
    public void For_AtEachBoundary_ReturnsTheExpectedStatus(
        string scenario, int? dueOffsetSeconds, int? windowMinutes, int? resolvedOffsetSeconds, SlaStatus expected)
    {
        var ticket = SlaStatusCases.Ticket(dueOffsetSeconds, windowMinutes, resolvedOffsetSeconds);
        var rule = SlaStatusRule.For(FixedClock.DefaultNow, SlaStatusCases.AtRiskPercent).Compile();

        Assert.True(expected == rule(ticket), $"{scenario}: expected {expected}, got {rule(ticket)}.");
    }

    [Fact]
    public void For_WithAZeroThreshold_IsOnlyAtRiskExactlyAtTheDueDate()
    {
        var rule = SlaStatusRule.For(FixedClock.DefaultNow, atRiskPercent: 0).Compile();

        Assert.Equal(SlaStatus.WithinSla, rule(SlaStatusCases.Ticket(1, SlaStatusCases.WindowMinutes, null)));
        Assert.Equal(SlaStatus.AtRisk, rule(SlaStatusCases.Ticket(0, SlaStatusCases.WindowMinutes, null)));
    }
}
