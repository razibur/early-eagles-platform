using EarlyEagles.Application.Abstractions.Announcements;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Announcements;
using Mediator;

namespace EarlyEagles.Application.Announcements;

public sealed class CreateAnnouncementCommandHandler : ICommandHandler<CreateAnnouncementCommand, CreateAnnouncementResult>
{
    private readonly IAnnouncementRepository announcements;
    private readonly ITenantContext tenantContext;
    private readonly TimeProvider timeProvider;

    public CreateAnnouncementCommandHandler(IAnnouncementRepository announcements, ITenantContext tenantContext, TimeProvider timeProvider)
    {
        this.announcements = announcements;
        this.tenantContext = tenantContext;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<CreateAnnouncementResult> Handle(CreateAnnouncementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var announcement = Announcement.Create(
            Guid.CreateVersion7(),
            tenantContext.TenantId,
            command.AuthorClerkUserId,
            command.Title,
            command.Body,
            timeProvider.GetUtcNow());
        announcements.Add(announcement);
        await announcements.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateAnnouncementResult(announcement.Id);
    }
}
