using FluentValidation;

namespace EarlyEagles.Application.Auditing;

/// <summary>Rejects out-of-range page sizes and tampered cursors before the handler runs.</summary>
public sealed class ListAuditEventsQueryValidator : AbstractValidator<ListAuditEventsQuery>
{
    public ListAuditEventsQueryValidator()
    {
        RuleFor(q => q.Limit)
            .InclusiveBetween(1, ListAuditEventsQuery.MaxLimit);

        RuleFor(q => q.Cursor)
            .Must(cursor => AuditEventCursor.TryDecode(cursor, out _))
            .When(q => q.Cursor is not null)
            .WithMessage("Cursor is not a valid page token. Pass back the nextCursor of a previous page unchanged.");
    }
}
