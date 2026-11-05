using EarlyEagles.Application.Abstractions.Announcements;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.Common;
using EarlyEagles.Domain.Announcements;
using Mediator;

namespace EarlyEagles.Application.Announcements;

public sealed class AddCommentCommandHandler : ICommandHandler<AddCommentCommand, AddCommentResult>
{
    private readonly IAnnouncementRepository announcements;
    private readonly ITenantContext tenantContext;
    private readonly TimeProvider timeProvider;

    public AddCommentCommandHandler(IAnnouncementRepository announcements, ITenantContext tenantContext, TimeProvider timeProvider)
    {
        this.announcements = announcements;
        this.tenantContext = tenantContext;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<AddCommentResult> Handle(AddCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await announcements.ExistsAsync(command.AnnouncementId, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundException("Announcement not found.");
        }

        var comment = AnnouncementComment.Create(
            Guid.CreateVersion7(),
            tenantContext.TenantId,
            command.AnnouncementId,
            command.AuthorClerkUserId,
            command.Body,
            timeProvider.GetUtcNow());
        announcements.AddComment(comment);
        await announcements.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AddCommentResult(comment.Id);
    }
}
