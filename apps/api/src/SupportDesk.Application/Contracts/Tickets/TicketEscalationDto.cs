using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>One escalation: priority, owner and due date before and after, and who did it and why.</summary>
public sealed record TicketEscalationDto(
    int Id,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>Response of POST /api/tickets/{id}/escalate: the updated ticket and the recorded escalation.</summary>
public sealed record EscalatedTicketDto(TicketDetailDto Ticket, TicketEscalationDto Escalation);
