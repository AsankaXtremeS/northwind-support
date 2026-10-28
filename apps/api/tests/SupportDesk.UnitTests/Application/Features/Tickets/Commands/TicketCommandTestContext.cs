using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// Shared arrangement for the ticket command tests: mocked repositories and queries, a fixed
/// clock, and command handlers wired to all of them.
/// </summary>
internal sealed class TicketCommandTestContext
{
    public TicketCommandTestContext()
    {
        // Lambdas, so a test can swap the customer, category or candidates after construction.
        Customers.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Customer);

        Categories.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Category);

        Agents.Setup(a => a.GetCandidatesAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Candidates);

        Tickets.Setup(t => t.NextReferenceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets.Setup(t => t.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        // Every command returns the ticket as re-read once its change has been saved.
        TicketQueries.Setup(q => q.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(1, "Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium),
                Category = new CategoryDto(1, "General", false)
            });

        // The history as re-read after an escalation; an unsaved escalation still has id 0.
        TicketQueries.Setup(q => q.GetEscalationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TicketEscalationDto(0, TicketPriority.Medium, TicketPriority.High, null, null,
                    null, FixedClock.DefaultNow.AddHours(8), "Customer is losing sales.", "Sara Lindqvist", FixedClock.DefaultNow)
            ]);

        // The agent the request is signed in as, the one an escalation is recorded against.
        CurrentUser.Setup(u => u.AgentId).Returns(SignedInAgentId);
        ExistingAgent(SignedInAgentId, "Sara Lindqvist");

        var getTicket = new GetTicketQueryHandler(TicketQueries.Object);

        Triage = new TicketTriage(Agents.Object, new SlaWindowPolicy(ShippedSlaTerms));

        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object, Customers.Object, Categories.Object, Triage, UnitOfWork.Object, Clock, getTicket,
            NullLogger<RaiseTicketCommandHandler>.Instance);

        EscalateTicket = new EscalateTicketCommandHandler(
            CurrentUser.Object, Agents.Object, Tickets.Object, Customers.Object, Categories.Object, Triage, UnitOfWork.Object, Clock, getTicket,
            TicketQueries.Object, NullLogger<EscalateTicketCommandHandler>.Instance);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(Tickets.Object, UnitOfWork.Object, Clock, getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object, Agents.Object, UnitOfWork.Object, Clock, getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);
    }

    /// <summary>The id the signed-in agent's token carries; well clear of the ids tests use for owners.</summary>
    public const int SignedInAgentId = 99;

    public Mock<ICurrentUser> CurrentUser { get; } = new();

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    public FixedClock Clock { get; } = new();

    /// <summary>The SLA terms shipped in appsettings.json.</summary>
    public static SlaTerms ShippedSlaTerms { get; } = new(
        new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        PremiumCustomerMultiplier: 0.5,
        MinimumWindowHours: 1);

    /// <summary>The customer every lookup returns; Standard tier unless a test changes it.</summary>
    public Customer Customer { get; set; } =
        new("Fabrikam Inc", "helpdesk@fabrikam.example", null, CustomerTier.Standard, FixedClock.DefaultNow);

    /// <summary>The category every lookup returns; no special rules unless a test changes it.</summary>
    public Category Category { get; set; } = new("General", requiresSpecialist: false, forcesCriticalPriority: false);

    /// <summary>The agents triage chooses from. Empty unless a test adds some.</summary>
    public List<AgentCandidate> Candidates { get; } = [];

    public TicketTriage Triage { get; }

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public EscalateTicketCommandHandler EscalateTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    /// <summary>The ticket handed to the repository by the last create call.</summary>
    public Ticket? AddedTicket { get; private set; }

    /// <summary>Makes <see cref="ITicketRepository.GetByIdAsync"/> return this ticket.</summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    public void ExistingAgent(int id, string fullName = "Alex Turner", bool isActive = true)
    {
        var agent = new Agent(fullName, $"agent{id}@example.com", maxOpenTickets: 10, Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents.Setup(a => a.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }

    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
}
