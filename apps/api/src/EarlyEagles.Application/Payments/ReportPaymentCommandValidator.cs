using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Tenants;
using FluentValidation;

namespace EarlyEagles.Application.Payments;

/// <summary>Turns a report the domain would refuse into a 400 before the handler runs.</summary>
public sealed class ReportPaymentCommandValidator : AbstractValidator<ReportPaymentCommand>
{
    public ReportPaymentCommandValidator()
    {
        RuleFor(c => c.ClerkUserId)
            .Must(TenantMember.IsValidClerkUserId);

        RuleFor(c => c.SenderName)
            .Must(SessionPayment.IsValidSenderName)
            .WithMessage($"senderName must be non-blank and at most {SessionPayment.SenderNameMaxLength} characters.");

        RuleFor(c => c.Reference)
            .Must(SessionPayment.IsValidReference)
            .WithMessage($"reference must be non-blank and at most {SessionPayment.ReferenceMaxLength} characters.");
    }
}
