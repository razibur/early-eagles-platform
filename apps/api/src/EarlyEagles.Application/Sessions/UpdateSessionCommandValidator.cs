using EarlyEagles.Domain.Sessions;
using FluentValidation;

namespace EarlyEagles.Application.Sessions;

/// <summary>The same rules as creating a session.</summary>
public sealed class UpdateSessionCommandValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionCommandValidator()
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
