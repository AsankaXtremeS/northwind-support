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

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    public void CanEscalate_ForAnOpenTicketBelowCritical_IsTrue(TicketPriority priority)
    {
        Assert.True(new TicketBuilder().WithPriority(priority).Build().CanEscalate);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.New)]
    [InlineData(TicketPriority.High, TicketStatus.Resolved)]
    [InlineData(TicketPriority.High, TicketStatus.Closed)]
    public void CanEscalate_ForACriticalOrFinishedTicket_IsFalse(TicketPriority priority, TicketStatus status)
    {
        Assert.False(new TicketBuilder().WithPriority(priority).WithStatus(status).Build().CanEscalate);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void NextPriority_IsOneLevelUp(TicketPriority current, TicketPriority expected)
    {
        Assert.Equal(expected, new TicketBuilder().WithPriority(current).Build().NextPriority());
    }

    [Fact]
    public void EnsureCanEscalate_WhenAlreadyCritical_ThrowsSayingSo()
    {
        var ticket = new TicketBuilder().WithReference("TCK-0009").WithPriority(TicketPriority.Critical).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(ticket.EnsureCanEscalate);

        Assert.Equal("Ticket TCK-0009 is already Critical and cannot be escalated further.", exception.Message);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void EnsureCanEscalate_WhenResolvedOrClosed_ThrowsSayingWhich(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithReference("TCK-0009").WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(ticket.EnsureCanEscalate);

        Assert.Equal($"Ticket TCK-0009 is {status} and cannot be escalated.", exception.Message);
    }

    [Fact]
    public void Escalate_RaisesThePriorityAndRestartsTheWindowFromNow()
    {
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .WithSla(FixedClock.DefaultNow.AddHours(24), windowMinutes: 1440)
            .Build();

        ticket.Escalate(Triage(TicketPriority.High, windowMinutes: 480, agentId: null), "Customer is losing sales.", "Sara", Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(480, ticket.SlaWindowMinutes);
        Assert.Equal(Later.AddMinutes(480), ticket.DueAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_RecordsWhatChangedWhoAndWhyTrimmed()
    {
        var originalDue = FixedClock.DefaultNow.AddHours(24);
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .AssignedTo(1)
            .WithSla(originalDue, windowMinutes: 1440)
            .Build();

        var escalation = ticket.Escalate(
            Triage(TicketPriority.High, windowMinutes: 480, agentId: 2), "  Customer is losing sales.  ", "  Sara  ", Later);

        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(1, escalation.FromAgentId);
        Assert.Equal(2, escalation.ToAgentId);
        Assert.Equal(originalDue, escalation.FromDueAtUtc);
        Assert.Equal(Later.AddMinutes(480), escalation.ToDueAtUtc);
        Assert.Equal("Customer is losing sales.", escalation.Reason);
        Assert.Equal("Sara", escalation.EscalatedBy);
        Assert.Equal(Later, escalation.EscalatedAtUtc);
        Assert.Same(escalation, Assert.Single(ticket.Escalations));
    }

    [Fact]
    public void Escalate_WithTriageForTheWrongPriority_ThrowsAndChangesNothing()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        Assert.Throws<ArgumentException>(() =>
            ticket.Escalate(Triage(TicketPriority.High, windowMinutes: 480, agentId: null), "Customer is losing sales.", "Sara", Later));

        Assert.Equal(TicketPriority.Low, ticket.Priority);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData("abcd")]
    [InlineData("   abcd   ")]
    public void Escalate_WithAReasonShorterThanFiveAfterTrimming_Throws(string reason)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        Assert.Throws<ArgumentException>(() =>
            ticket.Escalate(Triage(TicketPriority.Medium, windowMinutes: 1440, agentId: null), reason, "Sara", Later));
        Assert.Empty(ticket.Escalations);
    }

    [Fact]
    public void Escalate_WithAReasonLongerThanFiveHundred_Throws()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        Assert.Throws<ArgumentException>(() =>
            ticket.Escalate(Triage(TicketPriority.Medium, windowMinutes: 1440, agentId: null), new string('x', 501), "Sara", Later));
    }

    [Fact]
    public void Escalate_WithoutAnActor_Throws()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        Assert.ThrowsAny<ArgumentException>(() =>
            ticket.Escalate(Triage(TicketPriority.Medium, windowMinutes: 1440, agentId: null), "Customer is losing sales.", "  ", Later));
    }

    [Fact]
    public void Escalate_WhenAlreadyCritical_ThrowsAndRecordsNothing()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();

        Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(Triage(TicketPriority.Critical, windowMinutes: 240, agentId: null), "Customer is losing sales.", "Sara", Later));
        Assert.Empty(ticket.Escalations);
    }

    private static TriageDecision Triage(TicketPriority priority, int windowMinutes, int? agentId) =>
        new(new PriorityDecision(priority, "test"),
            new SlaWindow(windowMinutes, "test"),
            new AssignmentDecision(agentId, agentId is null ? null : "Agent", "test"));
}
