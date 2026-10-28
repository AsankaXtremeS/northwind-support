using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Sla;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, IOptions<SlaOptions> slaOptions) : ITicketQueries
{
    /// <remarks>
    /// Filters first, so the total count and the page both describe the filtered set; then
    /// sorts, pages and projects.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        // One "now" for the filter and the projection, so a ticket listed under a status shows it.
        var slaStatus = CurrentSlaStatus();
        var tickets = ApplyFilters(TicketsWithLabels(), query, slaStatus);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToListItem(slaStatus))
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var slaStatus = CurrentSlaStatus();
        var canEscalate = Ticket.CanEscalateRule;

        Expression<Func<TicketWithLabels, TicketDetailDto>> toDetail = x => new TicketDetailDto
        {
            Id = x.Ticket.Id,
            Reference = x.Ticket.Reference,
            Title = x.Ticket.Title,
            Description = x.Ticket.Description,
            Status = x.Ticket.Status,
            Priority = x.Ticket.Priority,
            Customer = new CustomerContactDto(
                x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
            Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
            AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
            CreatedAtUtc = x.Ticket.CreatedAtUtc,
            UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
            DueAtUtc = x.Ticket.DueAtUtc,
            ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
            SlaStatus = slaStatus.Inline(x.Ticket),
            CanEscalate = canEscalate.Inline(x.Ticket)
        };

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(toDetail.Expand())
            .FirstOrDefaultAsync(ct);
    }

    /// <remarks>
    /// Agent names are joined in, in the same query. Newest first, with the id breaking ties so
    /// two escalations in the same millisecond still come back in a stable order.
    /// </remarks>
    public async Task<IReadOnlyList<TicketEscalationDto>?> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        if (!await db.Set<Ticket>().AnyAsync(t => t.Id == ticketId, ct))
        {
            return null;
        }

        return await (
                from escalation in db.Set<TicketEscalation>().AsNoTracking()
                where escalation.TicketId == ticketId
                from fromAgent in db.Set<Agent>().Where(a => a.Id == escalation.FromAgentId).DefaultIfEmpty()
                from toAgent in db.Set<Agent>().Where(a => a.Id == escalation.ToAgentId).DefaultIfEmpty()
                orderby escalation.EscalatedAtUtc descending, escalation.Id descending
                select new TicketEscalationDto(
                    escalation.Id,
                    escalation.FromPriority,
                    escalation.ToPriority,
                    fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                    toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                    escalation.FromDueAtUtc,
                    escalation.ToDueAtUtc,
                    escalation.Reason,
                    escalation.EscalatedBy,
                    escalation.EscalatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct) =>
        await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(ToListItem(CurrentSlaStatus()))
            .ToListAsync(ct);

    /// <summary>The SLA rule as of now, with the configured at-risk threshold.</summary>
    private Expression<Func<Ticket, SlaStatus>> CurrentSlaStatus() =>
        SlaStatusRule.For(clock.UtcNow, slaOptions.Value.AtRiskThresholdPercent);

    /// <summary>The list row, shared by the ticket list and a customer's tickets.</summary>
    private static Expression<Func<TicketWithLabels, TicketListItemDto>> ToListItem(
        Expression<Func<Ticket, SlaStatus>> slaStatus)
    {
        Expression<Func<TicketWithLabels, TicketListItemDto>> toListItem = x => new TicketListItemDto
        {
            Id = x.Ticket.Id,
            Reference = x.Ticket.Reference,
            Title = x.Ticket.Title,
            Status = x.Ticket.Status,
            Priority = x.Ticket.Priority,
            Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
            Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
            AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
            CreatedAtUtc = x.Ticket.CreatedAtUtc,
            UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
            DueAtUtc = x.Ticket.DueAtUtc,
            ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
            SlaStatus = slaStatus.Inline(x.Ticket)
        };

        return toListItem.Expand();
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    /// <summary>
    /// Every list filter, one condition each. A filter left empty does not narrow the result;
    /// the ones supplied combine with AND.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source, TicketQuery query, Expression<Func<Ticket, SlaStatus>> slaStatus)
    {
        var search = query.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            source = source.Where(x =>
                x.Ticket.Title.Contains(search)
                || x.Ticket.Reference.Contains(search)
                || x.Customer.Name.Contains(search));
        }

        if (query.Status is { } status)
        {
            source = source.Where(x => x.Ticket.Status == status);
        }

        if (query.Priority is { } priority)
        {
            source = source.Where(x => x.Ticket.Priority == priority);
        }

        if (query.CategoryId is { } categoryId)
        {
            source = source.Where(x => x.Ticket.CategoryId == categoryId);
        }

        if (query.CustomerId is { } customerId)
        {
            source = source.Where(x => x.Ticket.CustomerId == customerId);
        }

        if (query.AssignedAgentId is { } assignedAgentId)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == assignedAgentId);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        if (query.SlaStatus is { } wantedSlaStatus)
        {
            Expression<Func<TicketWithLabels, bool>> hasSlaStatus = x => slaStatus.Inline(x.Ticket) == wantedSlaStatus;
            source = source.Where(hasSlaStatus.Expand());
        }

        return source;
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
