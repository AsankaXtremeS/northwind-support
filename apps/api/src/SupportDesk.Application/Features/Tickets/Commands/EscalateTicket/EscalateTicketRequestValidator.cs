using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Lengths are checked on the trimmed text, which is what gets stored, so padding with spaces
/// cannot get a too-short reason through.
/// </summary>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(reason => reason.Trim().Length is >= TicketEscalation.ReasonMinLength and <= TicketEscalation.ReasonMaxLength)
            .WithMessage($"Give a reason of {TicketEscalation.ReasonMinLength} to {TicketEscalation.ReasonMaxLength} characters.");

        RuleFor(x => x.EscalatedBy)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(actor => actor.Trim().Length <= TicketEscalation.EscalatedByMaxLength)
            .WithMessage($"Say who is escalating, in at most {TicketEscalation.EscalatedByMaxLength} characters.");
    }
}
