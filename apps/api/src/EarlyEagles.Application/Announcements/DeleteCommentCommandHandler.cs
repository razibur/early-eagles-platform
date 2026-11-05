using EarlyEagles.Application.Abstractions.Announcements;
using EarlyEagles.Application.Common;
using Mediator;

namespace EarlyEagles.Application.Announcements;

public sealed class DeleteCommentCommandHandler : ICommandHandler<DeleteCommentCommand>
{
    private readonly IAnnouncementRepository announcements;

    public DeleteCommentCommandHandler(IAnnouncementRepository announcements)
    {
        this.announcements = announcements;
    }

    public async ValueTask<Unit> Handle(DeleteCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var comment = await announcements.FindCommentAsync(command.AnnouncementId, command.CommentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Comment not found.");

        if (!command.ActorIsAdmin && comment.AuthorClerkUserId != command.ActorClerkUserId)
        {
            throw new ForbiddenException("Only the author or an admin can delete this comment.");
        }

        announcements.RemoveComment(comment);
        await announcements.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
