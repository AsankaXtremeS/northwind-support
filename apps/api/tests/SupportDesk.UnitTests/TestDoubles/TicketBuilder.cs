using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// Builds tickets for tests, so each test only has to say what actually matters to it. The
/// ticket is driven into the requested state through its own behaviour, so a test can never
/// start from a state the domain would not allow.
/// </summary>
public sealed class TicketBuilder
{
    private string _reference = "TCK-0001";
    private string _title = "Something is not working";
    private int _customerId = 1;
    private int _categoryId = 1;
    private TicketPriority _priority = TicketPriority.Medium;
    private TicketStatus _status = TicketStatus.New;
    private int? _assignedAgentId;
    private DateTime? _resolvedAtUtc;
    private DateTime? _dueAtUtc;
    private int? _slaWindowMinutes;

    public TicketBuilder WithReference(string reference)
    {
        _reference = reference;
        return this;
    }

    public TicketBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TicketBuilder ForCustomer(int customerId)
    {
        _customerId = customerId;
        return this;
    }

    public TicketBuilder InCategory(int categoryId)
    {
        _categoryId = categoryId;
        return this;
    }

    public TicketBuilder WithPriority(TicketPriority priority)
    {
        _priority = priority;
        return this;
    }

    public TicketBuilder WithStatus(TicketStatus status)
    {
        _status = status;
        return this;
    }

    public TicketBuilder AssignedTo(int? agentId)
    {
        _assignedAgentId = agentId;
        return this;
    }

    /// <summary>When the ticket reached its status; for Resolved and Closed, the resolution time.</summary>
    public TicketBuilder ResolvedAt(DateTime? resolvedAtUtc)
    {
        _resolvedAtUtc = resolvedAtUtc;
        return this;
    }

    /// <summary>
    /// Gives the ticket a due date and the SLA window it came from, through triage. A null window
    /// stands for a row from before windows were recorded, which no behaviour can produce, so
    /// that due date alone is set directly - as the seeder sets historical values through EF Core.
    /// </summary>
    public TicketBuilder WithSla(DateTime dueAtUtc, int? windowMinutes)
    {
        _dueAtUtc = dueAtUtc;
        _slaWindowMinutes = windowMinutes;
        return this;
    }

    public Ticket Build()
    {
        var ticket = Ticket.Raise(
            _reference,
            _title,
            "A description long enough to be realistic.",
            _customerId,
            _categoryId,
            _priority,
            FixedClock.DefaultNow);

        if (_assignedAgentId is not null)
        {
            ticket.AssignTo(_assignedAgentId, FixedClock.DefaultNow);
        }

        if (_dueAtUtc is { } due && _slaWindowMinutes is { } window)
        {
            // Triage starts the window "now", so start it where it has to end on the due date.
            ticket.ApplyTriage(
                new TriageDecision(
                    new PriorityDecision(_priority, "Built for a test."),
                    new SlaWindow(window, "Built for a test."),
                    new AssignmentDecision(_assignedAgentId, null, "Built for a test.")),
                due.AddMinutes(-window));
        }
        else if (_dueAtUtc is not null)
        {
            typeof(Ticket).GetProperty(nameof(Ticket.DueAtUtc))!.SetValue(ticket, _dueAtUtc);
        }

        if (_status != TicketStatus.New)
        {
            ticket.ChangeStatus(_status, _resolvedAtUtc ?? FixedClock.DefaultNow);
        }

        return ticket;
    }
}
