using FluentValidation;

namespace EarlyEagles.Application.Sessions;

/// <summary>Refuses a week that isn't a real <c>YYYY-Www</c>.</summary>
public sealed class ListSessionsQueryValidator : AbstractValidator<ListSessionsQuery>
{
    public ListSessionsQueryValidator()
    {
        RuleFor(q => q.Week)
            .Must(week => IsoWeek.TryParse(week, out _))
            .WithMessage("week must be an ISO week that exists, like 2026-W45.");
    }
}
