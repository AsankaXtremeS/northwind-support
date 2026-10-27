using Microsoft.Extensions.Options;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Sla;

/// <summary>
/// Rejects SLA settings that would produce nonsense due dates. Runs at startup, so a bad
/// configuration stops the application instead of quietly mis-dating tickets.
/// </summary>
public sealed class SlaOptionsValidator : IValidateOptions<SlaOptions>
{
    public ValidateOptionsResult Validate(string? name, SlaOptions options)
    {
        var failures = new List<string>();

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!options.BaseWindowHours.TryGetValue(priority, out var hours))
            {
                failures.Add($"{SlaOptions.SectionName}:BaseWindowHours has no window for {priority}.");
            }
            else if (hours <= 0)
            {
                failures.Add($"{SlaOptions.SectionName}:BaseWindowHours:{priority} must be greater than 0.");
            }
        }

        if (options.PremiumCustomerMultiplier is <= 0 or > 1)
        {
            failures.Add($"{SlaOptions.SectionName}:PremiumCustomerMultiplier must be greater than 0 and at most 1.");
        }

        if (options.MinimumWindowHours <= 0)
        {
            failures.Add($"{SlaOptions.SectionName}:MinimumWindowHours must be greater than 0.");
        }

        if (options.AtRiskThresholdPercent is < 0 or > 100)
        {
            failures.Add($"{SlaOptions.SectionName}:AtRiskThresholdPercent must be between 0 and 100.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
