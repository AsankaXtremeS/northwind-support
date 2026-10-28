namespace SupportDesk.Domain.Triage;

/// <summary>
/// Everything triage decided for a ticket - priority, SLA window and owner - each with the
/// reason behind it. Applied by <see cref="Aggregates.Tickets.Ticket.ApplyTriage"/>.
/// </summary>
public sealed record TriageDecision(PriorityDecision Priority, SlaWindow Window, AssignmentDecision Assignment);
