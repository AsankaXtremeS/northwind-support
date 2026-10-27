using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class SlaWindowPolicyTests
{
    /// <summary>The terms shipped in appsettings.json.</summary>
    private static readonly SlaWindowPolicy Shipped = Policy(
        low: 72, medium: 24, high: 8, critical: 4, premiumMultiplier: 0.5, minimumHours: 1);

    [Theory]
    [InlineData(TicketPriority.Low, 72 * 60)]
    [InlineData(TicketPriority.Medium, 24 * 60)]
    [InlineData(TicketPriority.High, 8 * 60)]
    [InlineData(TicketPriority.Critical, 4 * 60)]
    public void Calculate_ForAStandardCustomer_UsesTheBaseWindow(TicketPriority priority, int expectedMinutes)
    {
        var window = Shipped.Calculate(priority, CustomerTier.Standard);

        Assert.Equal(expectedMinutes, window.Minutes);
    }

    [Theory]
    [InlineData(TicketPriority.Low, 36 * 60)]
    [InlineData(TicketPriority.Medium, 12 * 60)]
    [InlineData(TicketPriority.High, 4 * 60)]
    [InlineData(TicketPriority.Critical, 2 * 60)]
    public void Calculate_ForAPremiumCustomer_AppliesTheMultiplier(TicketPriority priority, int expectedMinutes)
    {
        var window = Shipped.Calculate(priority, CustomerTier.Premium);

        Assert.Equal(expectedMinutes, window.Minutes);
    }

    [Fact]
    public void Calculate_WhenThePremiumWindowFallsBelowTheFloor_UsesTheFloor()
    {
        // 1.5 h halved is 45 min, under the 1 h floor.
        var policy = Policy(critical: 1.5, premiumMultiplier: 0.5, minimumHours: 1);

        var window = policy.Calculate(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(60, window.Minutes);
        Assert.Equal("Critical window 1 h 30 min × 0.5 for a Premium customer = 45 min, raised to the 1 h minimum.", window.Reason);
    }

    [Fact]
    public void Calculate_WhenThePremiumWindowIsExactlyTheFloor_KeepsItWithoutRaising()
    {
        var policy = Policy(critical: 2, premiumMultiplier: 0.5, minimumHours: 1);

        var window = policy.Calculate(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(60, window.Minutes);
        Assert.DoesNotContain("raised", window.Reason);
    }

    [Fact]
    public void Calculate_WhenAStandardWindowIsBelowTheFloor_UsesTheFloor()
    {
        var policy = Policy(critical: 0.5, premiumMultiplier: 0.5, minimumHours: 1);

        Assert.Equal(60, policy.Calculate(TicketPriority.Critical, CustomerTier.Standard).Minutes);
    }

    [Theory]
    [InlineData(1, 0.71, 43)]    // 42.6 min rounds up
    [InlineData(1, 0.704, 42)]   // 42.24 min rounds down
    [InlineData(1.5, 0.75, 68)]  // exactly 67.5 min: a half rounds away from zero
    public void Calculate_WithAFractionalResult_RoundsToWholeMinutes(double highHours, double premiumMultiplier, int expectedMinutes)
    {
        var policy = Policy(high: highHours, premiumMultiplier: premiumMultiplier, minimumHours: 0.5);

        Assert.Equal(expectedMinutes, policy.Calculate(TicketPriority.High, CustomerTier.Premium).Minutes);
    }

    [Fact]
    public void Calculate_ForAPremiumCustomer_ExplainsTheBaseWindowAndTheMultiplier()
    {
        var window = Shipped.Calculate(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal("Critical window 4 h × 0.5 for a Premium customer = 2 h.", window.Reason);
    }

    [Fact]
    public void Calculate_ForAStandardCustomer_ExplainsTheBaseWindow()
    {
        var window = Shipped.Calculate(TicketPriority.Medium, CustomerTier.Standard);

        Assert.Equal("Medium window 24 h.", window.Reason);
    }

    private static SlaWindowPolicy Policy(
        double low = 72, double medium = 24, double high = 8, double critical = 4,
        double premiumMultiplier = 0.5, double minimumHours = 1) =>
        new(new SlaTerms(
            new Dictionary<TicketPriority, double>
            {
                [TicketPriority.Low] = low,
                [TicketPriority.Medium] = medium,
                [TicketPriority.High] = high,
                [TicketPriority.Critical] = critical
            },
            premiumMultiplier,
            minimumHours));
}
