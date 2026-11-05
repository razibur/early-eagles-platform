namespace EarlyEagles.Application.Common;

/// <summary>
/// A valid request the current state refuses, like editing a cancelled session.
/// Becomes a 409 with this message, so write it for the person reading it.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException()
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
