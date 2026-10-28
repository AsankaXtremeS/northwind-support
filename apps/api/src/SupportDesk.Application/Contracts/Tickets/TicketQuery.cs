using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Filters, sorting and paging for the ticket list. Bound directly from the query string.
/// </summary>
/// <remarks>
/// Every filter left empty is ignored; the ones supplied combine with AND. So
/// <see cref="UnassignedOnly"/> together with an <see cref="AssignedAgentId"/> matches nothing
/// and returns an empty page.
/// </remarks>
public sealed record TicketQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Trimmed, then matched as a substring of the ticket title, its reference, or the customer
    /// name; blank is ignored. Partial references match too: "0042" finds TCK-0042, and would
    /// also find TCK-10042.
    /// </summary>
    public string? Search { get; init; }

    public TicketStatus? Status { get; init; }

    public TicketPriority? Priority { get; init; }

    public int? CategoryId { get; init; }

    public int? CustomerId { get; init; }

    public int? AssignedAgentId { get; init; }

    public bool UnassignedOnly { get; init; }

    /// <summary>Derived from the due date, the resolution and now; filtered in SQL like the rest.</summary>
    public SlaStatus? SlaStatus { get; init; }

    /// <summary>createdAtUtc | updatedAtUtc | dueAtUtc | priority | status.</summary>
    public string SortBy { get; init; } = "createdAtUtc";

    /// <summary>asc | desc.</summary>
    public string SortDirection { get; init; } = "desc";

    /// <summary>Page and page size clamped to something a database can serve.</summary>
    public TicketQuery Normalized() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize)
    };
}
