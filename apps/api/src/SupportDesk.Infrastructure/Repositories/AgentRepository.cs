using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>
    /// Uses the unique email index. SQL Server's default collation is case-insensitive, so
    /// "Sara.Lindqvist@..." finds the same agent.
    /// </summary>
    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Set<Agent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Email == email, ct);

    /// <summary>
    /// One SQL statement for every agent: the open-ticket count and the specialist check are
    /// correlated subqueries, so the cost does not grow with a query per agent. Inactive and
    /// full agents are included; deciding who is eligible is the assignment policy's job.
    /// </summary>
    public async Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(
        int categoryId, int? excludeTicketId, CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .Select(a => new AgentCandidate(
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed &&
                    t.Id != excludeTicketId),
                !db.Set<Category>().Any(c => c.Id == categoryId && c.RequiresSpecialist) ||
                a.Specializations.Any(s => s.CategoryId == categoryId)))
            .ToListAsync(ct);
}
