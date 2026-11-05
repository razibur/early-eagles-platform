namespace EarlyEagles.Application.Common;

/// <summary>
/// The thing a command names doesn't exist in this club. Becomes a 404, and an
/// audit row marked failed, which a result value wouldn't give.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException()
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
