using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Sla;

/// <summary>
/// The response-window rules, bound from the <c>Sla</c> section of configuration so they can
/// change without touching code. Checked at startup by <see cref="SlaOptionsValidator"/>.
/// </summary>
public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    /// <summary>How long the team has to respond, per priority, before any tier adjustment.</summary>
    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = [];

    /// <summary>Applied to the base window for Premium customers, e.g. 0.5 halves it.</summary>
    public double PremiumCustomerMultiplier { get; set; }

    /// <summary>No window is ever shorter than this, whatever the multiplier.</summary>
    public double MinimumWindowHours { get; set; }

    /// <summary>An open ticket with this share of its window or less remaining is at risk.</summary>
    public int AtRiskThresholdPercent { get; set; }
}
