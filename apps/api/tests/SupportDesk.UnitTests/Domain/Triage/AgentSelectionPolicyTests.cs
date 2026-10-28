using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class AgentSelectionPolicyTests
{
    [Fact]
    public void Choose_PicksTheEligibleAgentWithFewestOpenTickets()
    {
        var decision = AgentSelectionPolicy.Choose(
            [Agent(1, "Alex", open: 5), Agent(2, "Ben", open: 3), Agent(3, "Priya", open: 4)],
            currentAgentId: null);

        Assert.Equal(2, decision.AgentId);
        Assert.Equal("Ben", decision.AgentName);
        Assert.Equal("Ben has the fewest open tickets (3 of 10) of 3 agents eligible.", decision.Reason);
    }

    [Fact]
    public void Choose_WithATie_PicksTheLowestId()
    {
        var decision = AgentSelectionPolicy.Choose(
            [Agent(7, "Gail", open: 2), Agent(4, "Dan", open: 2), Agent(9, "Ivy", open: 2)],
            currentAgentId: null);

        Assert.Equal(4, decision.AgentId);
    }

    [Fact]
    public void Choose_InAnyCandidateOrder_PicksTheSameAgent()
    {
        AgentCandidate[] candidates = [Agent(3, "Cy", open: 1), Agent(1, "Al", open: 1), Agent(2, "Bo", open: 1)];

        var forwards = AgentSelectionPolicy.Choose(candidates, currentAgentId: null);
        var backwards = AgentSelectionPolicy.Choose(candidates.Reverse().ToList(), currentAgentId: null);

        Assert.Equal(1, forwards.AgentId);
        Assert.Equal(forwards.AgentId, backwards.AgentId);
    }

    [Fact]
    public void Choose_SkipsInactiveAgents()
    {
        var decision = AgentSelectionPolicy.Choose(
            [Agent(1, "Yuki", open: 0, isActive: false), Agent(2, "Ben", open: 3)],
            currentAgentId: null);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Choose_SkipsAgentsWhoDoNotMeetTheSpecialistRequirement()
    {
        var decision = AgentSelectionPolicy.Choose(
            [Agent(1, "Alex", open: 0, meetsSpecialistRequirement: false), Agent(2, "Priya", open: 4)],
            currentAgentId: null);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Choose_SkipsAgentsAtTheirLimitButNotOneBelowIt()
    {
        var decision = AgentSelectionPolicy.Choose(
            [Agent(1, "Marcus", open: 6, max: 6), Agent(2, "Sara", open: 5, max: 6)],
            currentAgentId: null);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Choose_WhenNobodyIsEligible_LeavesItUnassignedAndSaysWhy()
    {
        var decision = AgentSelectionPolicy.Choose(
            [
                Agent(1, "Yuki", open: 0, isActive: false),
                Agent(2, "Alex", open: 1, meetsSpecialistRequirement: false),
                Agent(3, "Ben", open: 2, meetsSpecialistRequirement: false),
                Agent(4, "Marcus", open: 6, max: 6)
            ],
            currentAgentId: null);

        Assert.Null(decision.AgentId);
        Assert.Null(decision.AgentName);
        Assert.Equal(
            "Unassigned: no agent is eligible (1 agent inactive, 2 agents not a specialist for this category, " +
            "1 agent at their limit of 6 open tickets).",
            decision.Reason);
    }

    [Fact]
    public void Choose_WithNoAgentsAtAll_LeavesItUnassigned()
    {
        var decision = AgentSelectionPolicy.Choose([], currentAgentId: null);

        Assert.Null(decision.AgentId);
        Assert.Equal("Unassigned: there are no agents.", decision.Reason);
    }

    [Fact]
    public void Choose_WhenTheCurrentAgentIsStillEligible_KeepsThemEvenIfSomeoneHasFewer()
    {
        var decision = AgentSelectionPolicy.Choose(
            [Agent(1, "Alex", open: 5), Agent(2, "Ben", open: 0)],
            currentAgentId: 1);

        Assert.Equal(1, decision.AgentId);
        Assert.Equal("Kept Alex, who is still eligible (5 of 10).", decision.Reason);
    }

    [Fact]
    public void Choose_WhenTheCurrentAgentIsNoLongerEligible_ReassignsAndSaysWhy()
    {
        var decision = AgentSelectionPolicy.Choose(
            [Agent(1, "Alex", open: 1, meetsSpecialistRequirement: false), Agent(2, "Priya", open: 4)],
            currentAgentId: 1);

        Assert.Equal(2, decision.AgentId);
        Assert.Equal(
            "Alex is not a specialist for this category. Priya has the fewest open tickets (4 of 10) of 1 agent eligible.",
            decision.Reason);
    }

    [Fact]
    public void Choose_WhenTheCurrentAgentIsIneligibleAndNobodyElseIs_LeavesItUnassigned()
    {
        var decision = AgentSelectionPolicy.Choose(
            [Agent(1, "Yuki", open: 0, isActive: false)],
            currentAgentId: 1);

        Assert.Null(decision.AgentId);
        Assert.StartsWith("Yuki is inactive. Unassigned:", decision.Reason);
    }

    [Fact]
    public void Choose_WhenTheCurrentAgentIsNotACandidate_PicksFromTheEligibleAgents()
    {
        var decision = AgentSelectionPolicy.Choose([Agent(2, "Ben", open: 3)], currentAgentId: 99);

        Assert.Equal(2, decision.AgentId);
    }

    private static AgentCandidate Agent(
        int id, string name, int open, int max = 10, bool isActive = true, bool meetsSpecialistRequirement = true) =>
        new(id, name, isActive, max, open, meetsSpecialistRequirement);
}
