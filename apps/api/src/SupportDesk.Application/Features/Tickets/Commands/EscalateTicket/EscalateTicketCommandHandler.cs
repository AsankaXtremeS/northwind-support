using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket one priority level. The SLA window restarts from now and the owner is
/// re-checked, through the same triage that raising a ticket uses, and the change is recorded.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ITicketQueries ticketQueries,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleViolationException">
    /// The ticket is resolved, closed or already Critical.
    /// </exception>
    public async Task<EscalatedTicketDto> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // Before any triage: a Critical ticket has no next priority to triage for.
        var toPriority = ticket.NextPriority();

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var decision = await triage.DecideAsync(
            new PriorityDecision(toPriority, $"Escalated from {ticket.Priority} to {toPriority}."),
            category,
            customer,
            currentAgentId: ticket.AssignedAgentId,
            excludeTicketId: ticket.Id,
            ct);

        var escalation = ticket.Escalate(decision, request.Reason, request.EscalatedBy, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated {From} -> {To} by {EscalatedBy}; agent {FromAgent} -> {ToAgent}.",
            ticket.Reference, escalation.FromPriority, escalation.ToPriority, escalation.EscalatedBy,
            escalation.FromAgentId, escalation.ToAgentId);

        var history = await ticketQueries.GetEscalationsAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        return new EscalatedTicketDto(
            await getTicket.HandleAsync(id, ct),
            history.First(recorded => recorded.Id == escalation.Id));
    }
}
