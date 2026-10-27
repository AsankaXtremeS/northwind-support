using System.Linq.Expressions;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private readonly List<TicketEscalation> _escalations = [];

    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    /// <summary>
    /// The length of the SLA window <see cref="DueAtUtc"/> was set from, in minutes. Kept because
    /// the window restarts on escalation, so it cannot be worked out from the creation time.
    /// Null when no SLA applies.
    /// </summary>
    public int? SlaWindowMinutes { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Whether the ticket can be escalated: still open and not already Critical. One definition,
    /// used here and translated to SQL where the read side reports it.
    /// </summary>
    public static Expression<Func<Ticket, bool>> CanEscalateRule { get; } = ticket =>
        ticket.Status != TicketStatus.Resolved &&
        ticket.Status != TicketStatus.Closed &&
        ticket.Priority != TicketPriority.Critical;

    // Declared after the rule: static initializers run in order, so the rule must exist first.
    private static readonly Func<Ticket, bool> CanEscalateCompiled = CanEscalateRule.Compile();

    /// <inheritdoc cref="CanEscalateRule"/>
    public bool CanEscalate => CanEscalateCompiled(this);

    /// <summary>Every escalation recorded against the ticket. Not loaded with the ticket.</summary>
    public IReadOnlyCollection<TicketEscalation> Escalations => _escalations;

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>, with no
    /// owner and no due date.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Takes on what triage decided: priority, owner (or none), and an SLA window that starts
    /// now, so the due date is <paramref name="nowUtc"/> plus the window.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is resolved or closed.</exception>
    public void ApplyTriage(TriageDecision triage, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is {Status} and cannot be triaged.");
        }

        Apply(triage, nowUtc);
    }

    /// <summary>
    /// Throws unless the ticket can be escalated, saying which rule stops it.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is resolved, closed or already Critical.</exception>
    public void EnsureCanEscalate()
    {
        if (!IsOpen)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is {Status} and cannot be escalated.");
        }

        if (Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is already Critical and cannot be escalated further.");
        }
    }

    /// <summary>The priority one level up from the current one.</summary>
    /// <exception cref="BusinessRuleViolationException">The ticket cannot be escalated.</exception>
    public TicketPriority NextPriority()
    {
        EnsureCanEscalate();

        return Priority + 1;
    }

    /// <summary>
    /// Raises the priority one level and takes on the re-triage done for it: an SLA window
    /// starting now, and an owner kept or changed. Records the change as a history row.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket cannot be escalated.</exception>
    /// <exception cref="ArgumentException">
    /// The triage is not for the next priority, or the reason or actor is missing or too long.
    /// </exception>
    public TicketEscalation Escalate(TriageDecision triage, string reason, string escalatedBy, DateTime nowUtc)
    {
        var toPriority = NextPriority();

        if (triage.Priority.Priority != toPriority)
        {
            throw new ArgumentException(
                $"Escalating {Reference} goes to {toPriority}, but the triage is for {triage.Priority.Priority}.",
                nameof(triage));
        }

        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (trimmedReason.Length is < TicketEscalation.ReasonMinLength or > TicketEscalation.ReasonMaxLength)
        {
            throw new ArgumentException(
                $"An escalation reason must be {TicketEscalation.ReasonMinLength}-{TicketEscalation.ReasonMaxLength} characters.",
                nameof(reason));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy);
        var trimmedActor = escalatedBy.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedActor.Length, TicketEscalation.EscalatedByMaxLength, nameof(escalatedBy));

        var (fromPriority, fromAgentId, fromDueAtUtc) = (Priority, AssignedAgentId, DueAtUtc);

        Apply(triage, nowUtc);

        var escalation = new TicketEscalation(
            Id, fromPriority, Priority, fromAgentId, AssignedAgentId, fromDueAtUtc, DueAtUtc!.Value,
            trimmedReason, trimmedActor, nowUtc);

        _escalations.Add(escalation);

        return escalation;
    }

    private void Apply(TriageDecision triage, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(triage.Window.Minutes);

        Priority = triage.Priority.Priority;
        AssignedAgentId = triage.Assignment.AgentId;
        SlaWindowMinutes = triage.Window.Minutes;
        DueAtUtc = nowUtc.AddMinutes(triage.Window.Minutes);
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }
}
