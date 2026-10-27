using System.Globalization;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// The configurable SLA terms (BR-6), in the units the configuration uses. Validated when the
/// application starts, so the policy can rely on a window for every priority.
/// </summary>
public sealed record SlaTerms(
    IReadOnlyDictionary<TicketPriority, double> BaseWindowHours,
    double PremiumCustomerMultiplier,
    double MinimumWindowHours);

/// <summary>A response window in whole minutes, and how it was worked out.</summary>
public sealed record SlaWindow(int Minutes, string Reason);

/// <summary>
/// BR-2 and BR-3: the base window for the priority, scaled for Premium customers, rounded to
/// whole minutes and then never shorter than the minimum.
/// </summary>
public sealed class SlaWindowPolicy(SlaTerms terms)
{
    private const int MinutesPerHour = 60;

    public SlaWindow Calculate(TicketPriority priority, CustomerTier tier)
    {
        var baseMinutes = terms.BaseWindowHours[priority] * MinutesPerHour;
        var reason = $"{priority} window {Describe(baseMinutes)}";

        var scaled = baseMinutes;
        if (tier == CustomerTier.Premium)
        {
            scaled = baseMinutes * terms.PremiumCustomerMultiplier;
            reason += $" × {terms.PremiumCustomerMultiplier.ToString(CultureInfo.InvariantCulture)} for a Premium customer";
        }

        var minutes = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        var floor = (int)Math.Round(terms.MinimumWindowHours * MinutesPerHour, MidpointRounding.AwayFromZero);

        if (minutes < floor)
        {
            return new SlaWindow(floor, $"{reason} = {Describe(minutes)}, raised to the {Describe(floor)} minimum.");
        }

        return new SlaWindow(minutes, tier == CustomerTier.Premium ? $"{reason} = {Describe(minutes)}." : $"{reason}.");
    }

    private static string Describe(double minutes)
    {
        var whole = (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
        var (hours, rest) = (whole / MinutesPerHour, whole % MinutesPerHour);

        return (hours, rest) switch
        {
            (0, _) => $"{rest} min",
            (_, 0) => $"{hours} h",
            _ => $"{hours} h {rest} min"
        };
    }
}
