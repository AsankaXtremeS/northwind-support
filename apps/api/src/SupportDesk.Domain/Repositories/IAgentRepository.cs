using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Every agent as a candidate for a ticket in <paramref name="categoryId"/>: whether they meet
    /// its specialist requirement and how many open tickets they hold, leaving out
    /// <paramref name="excludeTicketId"/> so a ticket never counts against its own owner.
    /// </summary>
    Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(int categoryId, int? excludeTicketId, CancellationToken ct);
}
