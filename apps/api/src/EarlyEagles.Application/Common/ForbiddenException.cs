namespace EarlyEagles.Application.Common;

/// <summary>
/// Signed in and in the right club, but this particular thing isn't theirs, a
/// rule a route policy can't express. Becomes a 403.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException()
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
