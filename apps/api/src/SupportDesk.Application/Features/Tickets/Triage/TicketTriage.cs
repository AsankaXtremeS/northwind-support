using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>
/// Works out a ticket's SLA window and owner once its priority is known. Shared by raising a
/// ticket (priority from <see cref="PriorityPolicy"/>) and escalating one (priority one step up),
/// so both follow exactly the same window and assignment rules.
/// </summary>
public sealed class TicketTriage(IAgentRepository agents, SlaWindowPolicy slaWindows)
{
    /// <param name="currentAgentId">The ticket's owner today, kept while still eligible.</param>
    /// <param name="excludeTicketId">The ticket being re-triaged, so it does not count against its owner.</param>
    public async Task<TriageDecision> DecideAsync(
        PriorityDecision priority,
        Category category,
        Customer customer,
        int? currentAgentId,
        int? excludeTicketId,
        CancellationToken ct)
    {
        var window = slaWindows.Calculate(priority.Priority, customer.Tier);
        var candidates = await agents.GetCandidatesAsync(category.Id, excludeTicketId, ct);
        var assignment = AgentSelectionPolicy.Choose(candidates, currentAgentId);

        return new TriageDecision(priority, window, assignment);
    }
}
