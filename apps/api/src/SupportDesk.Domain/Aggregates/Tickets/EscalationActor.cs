namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The signed-in agent who escalates a ticket. The id links the history row to the agent; the
/// name is kept as it was at the time, so the history still reads correctly if they are renamed.
/// </summary>
public sealed record EscalationActor(int AgentId, string Name);
