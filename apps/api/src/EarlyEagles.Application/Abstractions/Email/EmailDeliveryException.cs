namespace EarlyEagles.Application.Abstractions.Email;

/// <summary>
/// Thrown by <see cref="IEmailSender"/> when an email was not accepted.
/// <see cref="IsTransient"/> tells a consumer whether to retry (rate limit,
/// provider or network outage) or give up (the request itself is wrong, such
/// as an unverified sender domain or a malformed address).
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException()
        : base("The email was not accepted.")
    {
    }

    public EmailDeliveryException(string message)
        : base(message)
    {
    }

    public EmailDeliveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EmailDeliveryException(string message, int? statusCode, string? errorName, bool isTransient, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorName = errorName;
        IsTransient = isTransient;
    }

    /// <summary>The provider's HTTP status, or <see langword="null"/> when it was never reached.</summary>
    public int? StatusCode { get; }

    /// <summary>The provider's error code, e.g. Resend's <c>validation_error</c>.</summary>
    public string? ErrorName { get; }

    /// <summary>True when the same email may succeed if sent again later.</summary>
    public bool IsTransient { get; }
}
