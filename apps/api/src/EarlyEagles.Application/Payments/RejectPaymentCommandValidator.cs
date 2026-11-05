using EarlyEagles.Domain.Payments;
using FluentValidation;

namespace EarlyEagles.Application.Payments;

/// <summary>A rejection needs a reason the member can act on.</summary>
public sealed class RejectPaymentCommandValidator : AbstractValidator<RejectPaymentCommand>
{
    public RejectPaymentCommandValidator()
    {
        RuleFor(c => c.Reason)
            .Must(SessionPayment.IsValidRejectionReason)
            .WithMessage($"reason must be non-blank and at most {SessionPayment.RejectionReasonMaxLength} characters.");
    }
}
