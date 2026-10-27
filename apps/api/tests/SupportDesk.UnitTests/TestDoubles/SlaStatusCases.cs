using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The SLA status boundaries, shared by the compiled-rule tests and the SQL tests so both prove
/// the same thing. Offsets are seconds from <see cref="FixedClock.DefaultNow"/>. Every case
/// uses a 240-minute window and a 25% threshold, so a ticket is at risk from 3600 seconds
/// before its due date.
/// </summary>
public static class SlaStatusCases
{
    public const int WindowMinutes = 240;
    public const int AtRiskPercent = 25;
    public const int AtRiskSeconds = WindowMinutes * 60 * AtRiskPercent / 100;

    /// <summary>Scenario, due offset, recorded window, resolution offset (null = open), expected.</summary>
    public static TheoryData<string, int?, int?, int?, SlaStatus> All => new()
    {
        { "No due date", null, null, null, SlaStatus.NotApplicable },
        { "No due date, resolved", null, null, 0, SlaStatus.NotApplicable },
        { "Resolved before due", AtRiskSeconds, WindowMinutes, 0, SlaStatus.Met },
        { "Resolved exactly at due", 0, WindowMinutes, 0, SlaStatus.Met },
        { "Resolved one second late", 0, WindowMinutes, 1, SlaStatus.Breached },
        { "Open, one second past due", -1, WindowMinutes, null, SlaStatus.Breached },
        { "Open, exactly at due", 0, WindowMinutes, null, SlaStatus.AtRisk },
        { "Open, exactly the threshold left", AtRiskSeconds, WindowMinutes, null, SlaStatus.AtRisk },
        { "Open, one second more than the threshold left", AtRiskSeconds + 1, WindowMinutes, null, SlaStatus.WithinSla },
        { "Open, most of the window left", WindowMinutes * 60, WindowMinutes, null, SlaStatus.WithinSla },
        { "Open, due date but no recorded window", 60, null, null, SlaStatus.WithinSla }
    };

    public static Ticket Ticket(int? dueOffsetSeconds, int? windowMinutes, int? resolvedOffsetSeconds, int customerId = 1, int categoryId = 1)
    {
        var now = FixedClock.DefaultNow;
        var builder = new TicketBuilder().ForCustomer(customerId).InCategory(categoryId);

        if (resolvedOffsetSeconds is { } resolved)
        {
            builder.WithStatus(TicketStatus.Resolved).ResolvedAt(now.AddSeconds(resolved));
        }

        if (dueOffsetSeconds is { } due)
        {
            builder.WithSla(now.AddSeconds(due), windowMinutes);
        }

        return builder.Build();
    }
}
