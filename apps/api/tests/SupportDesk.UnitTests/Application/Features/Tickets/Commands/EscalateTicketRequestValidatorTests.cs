using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    [Theory]
    [InlineData("Lost")]                 // 4 characters
    [InlineData("   Lost   ")]           // 4 once trimmed
    [InlineData("")]
    [InlineData("     ")]
    public void Validate_WithAReasonShorterThanFiveOnceTrimmed_FailsOnReason(string reason)
    {
        var result = _validator.Validate(new EscalateTicketRequest(reason, "Sara Lindqvist"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void Validate_WithAReasonAtTheLengthLimits_Passes(int length)
    {
        var result = _validator.Validate(new EscalateTicketRequest(new string('x', length), "Sara Lindqvist"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithAReasonOfFiveHundredAndOne_FailsOnReason()
    {
        var result = _validator.Validate(new EscalateTicketRequest(new string('x', 501), "Sara Lindqvist"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithoutAnActor_FailsOnEscalatedBy(string? escalatedBy)
    {
        var result = _validator.Validate(new EscalateTicketRequest("Customer is losing sales.", escalatedBy!));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.EscalatedBy));
    }

    [Fact]
    public void Validate_WithAMissingReason_FailsWithoutThrowing()
    {
        var result = _validator.Validate(new EscalateTicketRequest(null!, "Sara Lindqvist"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validate_WithAnActorAroundTheLengthLimit_PassesOnlyUpToOneHundred(int length, bool expectedValid)
    {
        var result = _validator.Validate(new EscalateTicketRequest("Customer is losing sales.", new string('a', length)));

        Assert.Equal(expectedValid, result.IsValid);
    }
}
