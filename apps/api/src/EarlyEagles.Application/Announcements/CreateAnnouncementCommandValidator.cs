using EarlyEagles.Domain.Announcements;
using EarlyEagles.Domain.Tenants;
using FluentValidation;

namespace EarlyEagles.Application.Announcements;

/// <summary>Turns a post the domain would refuse into a 400 before the handler runs.</summary>
public sealed class CreateAnnouncementCommandValidator : AbstractValidator<CreateAnnouncementCommand>
{
    public CreateAnnouncementCommandValidator()
    {
        RuleFor(c => c.AuthorClerkUserId)
            .Must(TenantMember.IsValidClerkUserId);

        RuleFor(c => c.Title)
            .Must(Announcement.IsValidTitle)
            .WithMessage($"title must be non-blank and at most {Announcement.TitleMaxLength} characters.");

        RuleFor(c => c.Body)
            .Must(Announcement.IsValidBody)
            .WithMessage($"body must be non-blank and at most {Announcement.BodyMaxLength} characters.");
    }
}
