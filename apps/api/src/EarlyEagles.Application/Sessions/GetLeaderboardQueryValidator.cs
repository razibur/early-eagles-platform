using FluentValidation;

namespace EarlyEagles.Application.Sessions;

/// <summary>Refuses any period but <c>year</c>.</summary>
public sealed class GetLeaderboardQueryValidator : AbstractValidator<GetLeaderboardQuery>
{
    public GetLeaderboardQueryValidator()
    {
        RuleFor(q => q.Period)
            .Equal(GetLeaderboardQuery.Year)
            .WithMessage("period must be year.");
    }
}
