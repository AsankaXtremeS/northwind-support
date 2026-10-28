using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class PriorityPolicyTests
{
    private static readonly Category General = new("General", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Security = new("Security", requiresSpecialist: true, forcesCriticalPriority: true);

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void Decide_WithARequestedPriority_UsesIt(TicketPriority requested)
    {
        var decision = PriorityPolicy.Decide(requested, General);

        Assert.Equal(requested, decision.Priority);
        Assert.Contains("as requested", decision.Reason);
    }

    [Fact]
    public void Decide_WithoutARequestedPriority_DefaultsToMedium()
    {
        var decision = PriorityPolicy.Decide(null, General);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Contains("default", decision.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void Decide_ForACategoryThatForcesCritical_IsCriticalWhateverWasRequested(TicketPriority? requested)
    {
        var decision = PriorityPolicy.Decide(requested, Security);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
    }

    [Fact]
    public void Decide_ForACategoryThatForcesCritical_ExplainsWithTheCategoryNameAndTheRequest()
    {
        var decision = PriorityPolicy.Decide(TicketPriority.Low, Security);

        Assert.Equal("Security tickets are always Critical (requested Low).", decision.Reason);
    }
}
