namespace SupportDesk.Domain.Triage;

/// <summary>
/// An agent as the assignment rule sees them for one ticket.
/// <see cref="MeetsSpecialistRequirement"/> is true when the ticket's category needs no
/// specialist, or when the agent has that specialization.
/// </summary>
public sealed record AgentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    bool MeetsSpecialistRequirement)
{
    public bool IsEligible => IsActive && MeetsSpecialistRequirement && OpenTicketCount < MaxOpenTickets;

    /// <summary>Why the agent cannot take the ticket, or null when they can.</summary>
    public string? Ineligibility =>
        !IsActive ? "inactive"
        : !MeetsSpecialistRequirement ? "not a specialist for this category"
        : OpenTicketCount >= MaxOpenTickets ? $"at their limit of {MaxOpenTickets} open tickets"
        : null;
}

/// <summary>Who gets the ticket, if anyone, and why.</summary>
public sealed record AssignmentDecision(int? AgentId, string? AgentName, string Reason);

/// <summary>
/// BR-4 and BR-5: only eligible agents (active, specialised where the category requires it,
/// strictly below their open-ticket limit). The current owner keeps the ticket while still
/// eligible; otherwise the eligible agent with the fewest open tickets wins, lowest id on a tie.
/// Nobody eligible is a normal outcome: the ticket stays unassigned, with the reason.
/// </summary>
public static class AgentSelectionPolicy
{
    public static AssignmentDecision Choose(IReadOnlyCollection<AgentCandidate> candidates, int? currentAgentId)
    {
        var current = candidates.FirstOrDefault(c => c.Id == currentAgentId);

        if (current is { IsEligible: true })
        {
            return new AssignmentDecision(current.Id, current.FullName,
                $"Kept {current.FullName}, who is still eligible ({OpenTickets(current)}).");
        }

        var eligible = candidates.Where(c => c.IsEligible).ToList();
        var chosen = eligible.OrderBy(c => c.OpenTicketCount).ThenBy(c => c.Id).FirstOrDefault();
        var handover = current is null ? "" : $"{current.FullName} is {current.Ineligibility}. ";

        if (chosen is null)
        {
            return new AssignmentDecision(null, null, $"{handover}Unassigned: {NobodyEligible(candidates)}");
        }

        return new AssignmentDecision(chosen.Id, chosen.FullName,
            $"{handover}{chosen.FullName} has the fewest open tickets ({OpenTickets(chosen)}) " +
            $"of {Agents(eligible.Count)} eligible.");
    }

    private static string NobodyEligible(IReadOnlyCollection<AgentCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return "there are no agents.";
        }

        var reasons = candidates
            .GroupBy(c => c.Ineligibility)
            .Select(group => $"{Agents(group.Count())} {group.Key}");

        return $"no agent is eligible ({string.Join(", ", reasons)}).";
    }

    private static string OpenTickets(AgentCandidate agent) => $"{agent.OpenTicketCount} of {agent.MaxOpenTickets}";

    private static string Agents(int count) => count == 1 ? "1 agent" : $"{count} agents";
}
