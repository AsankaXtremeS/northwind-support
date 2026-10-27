using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>The priority a ticket gets, and why.</summary>
public sealed record PriorityDecision(TicketPriority Priority, string Reason);

/// <summary>
/// BR-1: the requested priority, or <see cref="Default"/> when none was asked for. A category
/// flagged <see cref="Category.ForcesCriticalPriority"/> is always Critical.
/// </summary>
public static class PriorityPolicy
{
    public const TicketPriority Default = TicketPriority.Medium;

    public static PriorityDecision Decide(TicketPriority? requested, Category category)
    {
        if (category.ForcesCriticalPriority)
        {
            var asked = requested is null ? "none requested" : $"requested {requested}";
            return new PriorityDecision(
                TicketPriority.Critical, $"{category.Name} tickets are always Critical ({asked}).");
        }

        return requested is { } priority
            ? new PriorityDecision(priority, $"{priority}, as requested.")
            : new PriorityDecision(Default, $"{Default}, the default when no priority is requested.");
    }
}
