using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>What triage decided for a new ticket, with a human-readable reason for each part.</summary>
public sealed record TriageDto(
    TicketPriority Priority,
    string PriorityReason,
    int SlaWindowMinutes,
    string SlaWindowReason,
    AgentSummaryDto? AssignedAgent,
    string AssignmentReason)
{
    public static TriageDto From(TriageDecision decision) => new(
        decision.Priority.Priority,
        decision.Priority.Reason,
        decision.Window.Minutes,
        decision.Window.Reason,
        decision.Assignment is { AgentId: { } id, AgentName: { } name } ? new AgentSummaryDto(id, name) : null,
        decision.Assignment.Reason);
}

/// <summary>Response of POST /api/tickets: the ticket as stored, and how it was triaged.</summary>
public sealed record RaisedTicketDto(TicketDetailDto Ticket, TriageDto Triage);
