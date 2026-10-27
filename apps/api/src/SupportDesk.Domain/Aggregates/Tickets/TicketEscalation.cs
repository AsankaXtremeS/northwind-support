using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// One escalation of a ticket: what changed, who did it and why. Part of the
/// <see cref="Ticket"/> aggregate and only ever created by <see cref="Ticket.Escalate"/>.
/// Immutable: a history row is never edited.
/// </summary>
public sealed class TicketEscalation : Entity
{
    public const int ReasonMinLength = 5;
    public const int ReasonMaxLength = 500;
    /// <summary>The same as an agent's full name, which is what gets recorded.</summary>
    public const int EscalatedByMaxLength = 200;

    private TicketEscalation()
    {
        // For EF Core.
    }

    internal TicketEscalation(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? fromDueAtUtc,
        DateTime toDueAtUtc,
        string reason,
        int escalatedByAgentId,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        FromAgentId = fromAgentId;
        ToAgentId = toAgentId;
        FromDueAtUtc = fromDueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        Reason = reason;
        EscalatedByAgentId = escalatedByAgentId;
        EscalatedBy = escalatedBy;
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    public int? FromAgentId { get; private set; }

    public int? ToAgentId { get; private set; }

    /// <summary>Null when the ticket had no due date before, e.g. one raised before triage existed.</summary>
    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime ToDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// The agent who escalated, taken from their sign-in. Null only for rows recorded before
    /// sign-in existed, when the name was typed in.
    /// </summary>
    public int? EscalatedByAgentId { get; private set; }

    /// <summary>The escalating agent's name at the time.</summary>
    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }
}
