using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Domain.Announcements;

/// <summary>A member's reply to an announcement (P1-001.4). Deleting one removes the row; the audit log keeps the fact.</summary>
public sealed class AnnouncementComment : AggregateRoot<Guid>, ITenantScoped
{
    public const int BodyMaxLength = 2000;

    private AnnouncementComment(Guid id, Guid tenantId, Guid announcementId, string authorClerkUserId, string body, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        AnnouncementId = announcementId;
        AuthorClerkUserId = authorClerkUserId;
        Body = body;
        CreatedAt = createdAt;
    }

    // Parameterless ctor for EF Core materialisation.
    private AnnouncementComment()
    {
        AuthorClerkUserId = string.Empty;
        Body = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public Guid AnnouncementId { get; private set; }

    public string AuthorClerkUserId { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AnnouncementComment Create(Guid id, Guid tenantId, Guid announcementId, string authorClerkUserId, string body, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Comment id must not be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        if (announcementId == Guid.Empty)
        {
            throw new ArgumentException("Announcement id must not be empty.", nameof(announcementId));
        }

        if (!TenantMember.IsValidClerkUserId(authorClerkUserId))
        {
            throw new ArgumentException("Author is not valid.", nameof(authorClerkUserId));
        }

        if (!IsValidBody(body))
        {
            throw new ArgumentException($"Body must be non-blank and at most {BodyMaxLength} characters.", nameof(body));
        }

        return new AnnouncementComment(id, tenantId, announcementId, authorClerkUserId, body.Trim(), createdAt);
    }

    /// <summary>True when <paramref name="body"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidBody(string? body) =>
        body is not null && body.Trim().Length is > 0 and <= BodyMaxLength;
}
