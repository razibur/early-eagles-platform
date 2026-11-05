using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Domain.Announcements;

/// <summary>A post from an admin to the whole club (P1-001.4).</summary>
public sealed class Announcement : AggregateRoot<Guid>, ITenantScoped
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 5000;

    private Announcement(Guid id, Guid tenantId, string authorClerkUserId, string title, string body, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        AuthorClerkUserId = authorClerkUserId;
        Title = title;
        Body = body;
        CreatedAt = createdAt;
    }

    // Parameterless ctor for EF Core materialisation.
    private Announcement()
    {
        AuthorClerkUserId = string.Empty;
        Title = string.Empty;
        Body = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public string AuthorClerkUserId { get; private set; }

    public string Title { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Announcement Create(Guid id, Guid tenantId, string authorClerkUserId, string title, string body, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Announcement id must not be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        if (!TenantMember.IsValidClerkUserId(authorClerkUserId))
        {
            throw new ArgumentException("Author is not valid.", nameof(authorClerkUserId));
        }

        if (!IsValidTitle(title))
        {
            throw new ArgumentException($"Title must be non-blank and at most {TitleMaxLength} characters.", nameof(title));
        }

        if (!IsValidBody(body))
        {
            throw new ArgumentException($"Body must be non-blank and at most {BodyMaxLength} characters.", nameof(body));
        }

        return new Announcement(id, tenantId, authorClerkUserId, title.Trim(), body.Trim(), createdAt);
    }

    /// <summary>True when <paramref name="title"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidTitle(string? title) =>
        title is not null && title.Trim().Length is > 0 and <= TitleMaxLength;

    /// <summary>True when <paramref name="body"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidBody(string? body) =>
        body is not null && body.Trim().Length is > 0 and <= BodyMaxLength;
}
