using FluentValidation;

namespace EarlyEagles.Application.Announcements;

/// <summary>Rejects out-of-range page sizes and tampered cursors before the handler runs.</summary>
public sealed class ListAnnouncementsQueryValidator : AbstractValidator<ListAnnouncementsQuery>
{
    public ListAnnouncementsQueryValidator()
    {
        RuleFor(q => q.Limit)
            .InclusiveBetween(1, ListAnnouncementsQuery.MaxLimit);

        RuleFor(q => q.Cursor)
            .Must(cursor => AnnouncementCursor.TryDecode(cursor, out _))
            .When(q => q.Cursor is not null)
            .WithMessage("Cursor is not a valid page token. Pass back the nextCursor of a previous page unchanged.");
    }
}
