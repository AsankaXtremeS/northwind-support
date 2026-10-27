using SupportDesk.Application.Sla;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Sla;

public class SlaOptionsValidatorTests
{
    private readonly SlaOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithTheShippedSettings_Succeeds()
    {
        var result = _validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void Validate_WithAPriorityMissing_FailsNamingThatPriority(TicketPriority missing)
    {
        var options = ValidOptions();
        options.BaseWindowHours.Remove(missing);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains($"no window for {missing}"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithANonPositiveWindow_Fails(double hours)
    {
        var options = ValidOptions();
        options.BaseWindowHours[TicketPriority.High] = hours;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("BaseWindowHours:High"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.01)]
    public void Validate_WithAMultiplierOutsideZeroToOne_Fails(double multiplier)
    {
        var options = ValidOptions();
        options.PremiumCustomerMultiplier = multiplier;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("PremiumCustomerMultiplier"));
    }

    [Fact]
    public void Validate_WithAMultiplierOfExactlyOne_Succeeds()
    {
        var options = ValidOptions();
        options.PremiumCustomerMultiplier = 1;

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithANonPositiveFloor_Fails(double floorHours)
    {
        var options = ValidOptions();
        options.MinimumWindowHours = floorHours;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("MinimumWindowHours"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WithAThresholdOutsideZeroToHundred_Fails(int percent)
    {
        var options = ValidOptions();
        options.AtRiskThresholdPercent = percent;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("AtRiskThresholdPercent"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Validate_WithAThresholdAtItsLimits_Succeeds(int percent)
    {
        var options = ValidOptions();
        options.AtRiskThresholdPercent = percent;

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void Validate_WithSeveralProblems_ReportsEachOne()
    {
        var options = new SlaOptions();

        var result = _validator.Validate(null, options);

        // Four missing windows, the multiplier and the floor. A threshold of 0 is allowed.
        Assert.Equal(6, result.Failures!.Count());
    }

    [Fact]
    public void ToTerms_CopiesTheWindowTermsSoLaterEditsDoNotLeakIn()
    {
        var options = ValidOptions();

        var terms = options.ToTerms();
        options.BaseWindowHours[TicketPriority.High] = 99;

        Assert.Equal(8, terms.BaseWindowHours[TicketPriority.High]);
        Assert.Equal(0.5, terms.PremiumCustomerMultiplier);
        Assert.Equal(1, terms.MinimumWindowHours);
    }

    /// <summary>The values shipped in appsettings.json.</summary>
    private static SlaOptions ValidOptions() => new()
    {
        BaseWindowHours = new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };
}
