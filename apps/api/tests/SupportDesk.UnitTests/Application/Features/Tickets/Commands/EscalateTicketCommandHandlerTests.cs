using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static readonly EscalateTicketRequest Request = new("Customer is losing sales.");

    [Fact]
    public async Task HandleAsync_RaisesThePriorityOneLevelAndRestartsTheWindowFromNow()
    {
        var context = new TicketCommandTestContext();
        context.Clock.Advance(TimeSpan.FromHours(5));
        var ticket = context.ExistingTicket(MediumTicket());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(8 * 60, ticket.SlaWindowMinutes);
        Assert.Equal(context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ForAPremiumCustomer_HalvesTheNewWindow()
    {
        var context = new TicketCommandTestContext
        {
            Customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, FixedClock.DefaultNow)
        };
        var ticket = context.ExistingTicket(MediumTicket());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(4 * 60, ticket.SlaWindowMinutes);
        Assert.Equal(context.Clock.UtcNow.AddHours(4), ticket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenTheOwnerIsStillEligible_KeepsThemEvenIfSomeoneHasFewer()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.AddRange([Candidate(1, "Alex Turner", open: 5), Candidate(2, "Ben Osei", open: 0)]);
        var ticket = context.ExistingTicket(MediumTicket(agentId: 1));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(1, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenTheOwnerIsNoLongerEligible_Reassigns()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.AddRange([Candidate(1, "Yuki Tanaka", open: 0, isActive: false), Candidate(2, "Ben Osei", open: 3)]);
        var ticket = context.ExistingTicket(MediumTicket(agentId: 1));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenUnassigned_AssignsPerTheUsualRule()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.AddRange([Candidate(1, "Alex Turner", open: 5), Candidate(2, "Ben Osei", open: 3)]);
        var ticket = context.ExistingTicket(MediumTicket());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_AsksForCandidatesExcludingTheTicketItself()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(MediumTicket(agentId: 1));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        context.Agents.Verify(
            a => a.GetCandidatesAsync(context.Category.Id, ticket.Id, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleAsync_RecordsTheEscalationOnTheTicket()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(MediumTicket(agentId: 1));
        var originalDue = ticket.DueAtUtc;

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        var escalation = Assert.Single(ticket.Escalations);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(1, escalation.FromAgentId);
        Assert.Equal(originalDue, escalation.FromDueAtUtc);
        Assert.Equal(ticket.DueAtUtc, escalation.ToDueAtUtc);
        Assert.Equal("Customer is losing sales.", escalation.Reason);
        Assert.Equal(TicketCommandTestContext.SignedInAgentId, escalation.EscalatedByAgentId);
        Assert.Equal("Sara Lindqvist", escalation.EscalatedBy);
        Assert.Equal(context.Clock.UtcNow, escalation.EscalatedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheTicketAndTheRecordedEscalation()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(MediumTicket());

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal("TCK-0041", result.Ticket.Reference);
        Assert.Equal("Customer is losing sales.", result.Escalation.Reason);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyCritical_ThrowsSayingSoAndDoesNotSave()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithReference("TCK-0009").WithPriority(TicketPriority.Critical).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None));

        Assert.Contains("already Critical", exception.Message);
        context.VerifySaved(Times.Never());
        context.Agents.Verify(
            a => a.GetCandidatesAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_WhenResolvedOrClosed_ThrowsSayingWhichAndDoesNotSave(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Low).WithStatus(status).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None));

        Assert.Contains($"is {status}", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_SignedInAgentInactive_ThrowsNotAuthenticatedAndDoesNotSave()
    {
        var context = new TicketCommandTestContext();
        context.ExistingAgent(TicketCommandTestContext.SignedInAgentId, "Sara Lindqvist", isActive: false);
        var ticket = context.ExistingTicket(MediumTicket());

        await Assert.ThrowsAsync<NotAuthenticatedException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None));

        Assert.Empty(ticket.Escalations);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_SignedInAgentNoLongerExists_ThrowsNotAuthenticated()
    {
        var context = new TicketCommandTestContext();
        context.CurrentUser.Setup(u => u.AgentId).Returns(12345);
        var ticket = context.ExistingTicket(MediumTicket());

        await Assert.ThrowsAsync<NotAuthenticatedException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTheTicketDoesNotExist_ThrowsNotFound()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(404, Request, CancellationToken.None));
        context.VerifySaved(Times.Never());
    }

    private static Ticket MediumTicket(int? agentId = null) =>
        new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .AssignedTo(agentId)
            .WithSla(FixedClock.DefaultNow.AddHours(24), windowMinutes: 1440)
            .Build();

    private static AgentCandidate Candidate(int id, string name, int open, bool isActive = true) =>
        new(id, name, isActive, MaxOpenTickets: 10, open, MeetsSpecialistRequirement: true);
}
