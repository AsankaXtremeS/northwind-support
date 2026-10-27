using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_ForACategoryThatForcesCritical_RaisesItAsCritical()
    {
        var context = new TicketCommandTestContext
        {
            Category = new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true)
        };

        var raised = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
        Assert.Equal("Security tickets are always Critical (requested Low).", raised.Triage.PriorityReason);
    }

    [Fact]
    public async Task HandleAsync_ForAStandardCustomer_SetsTheBaseWindowFromNow()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal(8 * 60, context.AddedTicket!.SlaWindowMinutes);
        Assert.Equal(FixedClock.DefaultNow.AddHours(8), context.AddedTicket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ForAPremiumCustomer_HalvesTheWindow()
    {
        var context = new TicketCommandTestContext
        {
            Customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, FixedClock.DefaultNow)
        };

        var raised = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Critical), CancellationToken.None);

        Assert.Equal(2 * 60, context.AddedTicket!.SlaWindowMinutes);
        Assert.Equal(FixedClock.DefaultNow.AddHours(2), context.AddedTicket.DueAtUtc);
        Assert.Equal(2 * 60, raised.Triage.SlaWindowMinutes);
        Assert.Equal("Critical window 4 h × 0.5 for a Premium customer = 2 h.", raised.Triage.SlaWindowReason);
    }

    [Fact]
    public async Task HandleAsync_AssignsTheEligibleAgentWithFewestOpenTickets()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.AddRange([Candidate(1, "Alex Turner", open: 5), Candidate(2, "Ben Osei", open: 3)]);

        var raised = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(2, context.AddedTicket!.AssignedAgentId);
        Assert.Equal(new AgentSummaryDto(2, "Ben Osei"), raised.Triage.AssignedAgent);
        Assert.Contains("Ben Osei has the fewest open tickets", raised.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyIsEligible_RaisesItUnassignedWithTheReason()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.Add(Candidate(5, "Marcus Doyle", open: 6, max: 6));

        var raised = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Null(context.AddedTicket!.AssignedAgentId);
        Assert.Null(raised.Triage.AssignedAgent);
        Assert.StartsWith("Unassigned:", raised.Triage.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_AsksForCandidatesForTheTicketsCategoryWithNothingExcluded()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        context.Agents.Verify(a => a.GetCandidatesAsync(context.Category.Id, null, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheStoredTicketAndTheTriage()
    {
        var context = new TicketCommandTestContext();

        var raised = await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal("TCK-0041", raised.Ticket.Reference);
        Assert.Equal(TicketPriority.High, raised.Triage.Priority);
        Assert.Equal("High, as requested.", raised.Triage.PriorityReason);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryIsInactive_ThrowsConflictAndDoesNotSave()
    {
        var category = new Category("Legacy", requiresSpecialist: false, forcesCriticalPriority: false);
        category.Deactivate();
        var context = new TicketCommandTestContext { Category = category };

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Equal("Category Legacy is inactive and cannot take new tickets.", exception.Message);
        context.VerifySaved(Times.Never());
    }

    private static AgentCandidate Candidate(int id, string name, int open, int max = 10) =>
        new(id, name, IsActive: true, max, open, MeetsSpecialistRequirement: true);
}
