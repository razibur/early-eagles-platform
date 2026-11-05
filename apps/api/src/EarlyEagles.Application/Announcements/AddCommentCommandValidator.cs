using EarlyEagles.Domain.Announcements;
using EarlyEagles.Domain.Tenants;
using FluentValidation;

namespace EarlyEagles.Application.Announcements;

/// <summary>Turns a comment the domain would refuse into a 400 before the handler runs.</summary>
public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(c => c.AuthorClerkUserId)
            .Must(TenantMember.IsValidClerkUserId);

        RuleFor(c => c.Body)
            .Must(AnnouncementComment.IsValidBody)
            .WithMessage($"body must be non-blank and at most {AnnouncementComment.BodyMaxLength} characters.");
    }
}
