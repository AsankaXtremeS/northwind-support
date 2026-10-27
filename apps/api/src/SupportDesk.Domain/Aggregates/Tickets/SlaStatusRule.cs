using System.Linq.Expressions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Where a ticket stands against its SLA. The one definition of the rule: queries translate it
/// to SQL, so the list can be filtered and paged by it, and tests compile it to C#.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>No due date: <see cref="SlaStatus.NotApplicable"/>.</item>
/// <item>Resolved on or before the due date: <see cref="SlaStatus.Met"/>; resolved later: <see cref="SlaStatus.Breached"/>.</item>
/// <item>Unresolved and past the due date: <see cref="SlaStatus.Breached"/>.</item>
/// <item>Unresolved with <paramref name="atRiskPercent"/>% of the window or less left, including
/// exactly at the due date: <see cref="SlaStatus.AtRisk"/>.</item>
/// <item>Otherwise <see cref="SlaStatus.WithinSla"/>. A ticket with a due date but no recorded
/// window is never at risk; it goes straight from within the SLA to breached.</item>
/// </list>
/// Date arithmetic is applied to "now", never to a column, and the at-risk margin is whole
/// seconds from integer division, so SQL and C# agree to the second.
/// </remarks>
public static class SlaStatusRule
{
    private const int SecondsPerMinute = 60;
    private const int PercentBase = 100;

    public static Expression<Func<Ticket, SlaStatus>> For(DateTime nowUtc, int atRiskPercent) =>
        ticket =>
            ticket.DueAtUtc == null ? SlaStatus.NotApplicable
            : ticket.ResolvedAtUtc != null
                ? (ticket.ResolvedAtUtc <= ticket.DueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
            : nowUtc > ticket.DueAtUtc ? SlaStatus.Breached
            : ticket.SlaWindowMinutes != null
              && ticket.DueAtUtc <= nowUtc.AddSeconds(
                  ticket.SlaWindowMinutes.Value * SecondsPerMinute * atRiskPercent / PercentBase)
                ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
}
