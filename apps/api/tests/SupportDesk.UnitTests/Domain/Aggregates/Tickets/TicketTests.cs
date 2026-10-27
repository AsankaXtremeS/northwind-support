using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);

    [Fact]
    public void Raise_StartsNewUnassignedAndWithoutADueDate()
    {
        var ticket = Ticket.Raise(
            "TCK-0041", "  Cannot log in  ", "  Happens for every user.  ", 1, 3,
            TicketPriority.High, FixedClock.DefaultNow);

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal("Happens for every user.", ticket.Description);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenResolving_StampsResolutionAndUpdateTimes()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).Build();

        ticket.ChangeStatus(TicketStatus.Resolved, Later);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Later, ticket.ResolvedAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
        Assert.False(ticket.IsOpen);
    }

    [Fact]
    public void ChangeStatus_WhenReopening_ClearsTheResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.InProgress, Later);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAnUnresolvedTicket_RecordsAResolutionTime()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(Later, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAResolvedTicket_KeepsTheOriginalResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(FixedClock.DefaultNow, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenTicketIsAlreadyClosed_Throws()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0026")
            .WithStatus(TicketStatus.Closed)
            .Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.ChangeStatus(TicketStatus.Open, Later));

        Assert.Contains("TCK-0026", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void AssignTo_WithoutAnAgent_TakesTheTicketBack()
    {
        var ticket = new TicketBuilder().AssignedTo(2).Build();

        ticket.AssignTo(null, Later);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyTriage_SetsPriorityOwnerWindowAndADueDateFromNow()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        ticket.ApplyTriage(Triage(TicketPriority.High, windowMinutes: 480, agentId: 2), Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(480, ticket.SlaWindowMinutes);
        Assert.Equal(Later.AddMinutes(480), ticket.DueAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyTriage_WithNobodyToAssign_LeavesTheTicketUnassigned()
    {
        var ticket = new TicketBuilder().AssignedTo(3).Build();

        ticket.ApplyTriage(Triage(TicketPriority.Medium, windowMinutes: 1440, agentId: null), Later);

        Assert.Null(ticket.AssignedAgentId);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void ApplyTriage_WhenTheTicketIsNoLongerOpen_Throws(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithStatus(status).Build();

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.ApplyTriage(Triage(TicketPriority.High, windowMinutes: 480, agentId: null), Later));
        Assert.Null(ticket.DueAtUtc);
    }

    private static TriageDecision Triage(TicketPriority priority, int windowMinutes, int? agentId) =>
        new(new PriorityDecision(priority, "test"),
            new SlaWindow(windowMinutes, "test"),
            new AssignmentDecision(agentId, agentId is null ? null : "Agent", "test"));
}
