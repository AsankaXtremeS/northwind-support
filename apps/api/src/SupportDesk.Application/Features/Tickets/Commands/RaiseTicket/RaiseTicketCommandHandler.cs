using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer and triages it: priority, SLA window and owner are decided
/// here rather than by hand. Nobody being free to take it is a normal outcome, not an error.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    /// <exception cref="ConflictException">The category is inactive.</exception>
    public async Task<RaisedTicketDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        if (!category.IsActive)
        {
            throw new ConflictException($"Category {category.Name} is inactive and cannot take new tickets.");
        }

        var priority = PriorityPolicy.Decide(request.RequestedPriority, category);
        var decision = await triage.DecideAsync(
            priority, category, customer, currentAgentId: null, excludeTicketId: null, ct);

        var now = clock.UtcNow;

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            decision.Priority.Priority,
            now);

        ticket.ApplyTriage(decision, now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}: {Priority}, due {DueAtUtc}, agent {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.DueAtUtc, ticket.AssignedAgentId);

        return new RaisedTicketDto(await getTicket.HandleAsync(ticket.Id, ct), TriageDto.From(decision));
    }
}
