using EarlyEagles.Domain.Sessions;
using FluentValidation;

namespace EarlyEagles.Application.Sessions;

/// <summary>Turns a session the domain would refuse into a 400 before the handler runs.</summary>
public sealed class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(c => c.Date)
            .NotNull()
            .WithMessage("date is required, as YYYY-MM-DD.");

        RuleFor(c => c.StartTime)
            .NotNull()
            .WithMessage("startTime is required, as HH:mm.");

        RuleFor(c => c.Venue)
            .Must(Session.IsValidVenue)
            .WithMessage($"venue must be non-blank and at most {Session.VenueMaxLength} characters.");

        RuleFor(c => c.FeeCents)
            .Must(fee => fee is null || Session.IsValidFee(fee.Value))
            .WithMessage($"feeCents must be between 0 and {Session.MaxFeeCents}.");
    }
}
