using EarlyEagles.Domain.Tenants;
using FluentValidation;

namespace EarlyEagles.Application.Sessions;

/// <summary>Refuses a missing or unknown answer with a 400 before the handler runs.</summary>
public sealed class SetRsvpCommandValidator : AbstractValidator<SetRsvpCommand>
{
    public SetRsvpCommandValidator()
    {
        RuleFor(c => c.ClerkUserId)
            .Must(TenantMember.IsValidClerkUserId);

        RuleFor(c => c.Response)
            .NotNull()
            .IsInEnum()
            .WithMessage("response must be Yes, No or Maybe.");
    }
}
