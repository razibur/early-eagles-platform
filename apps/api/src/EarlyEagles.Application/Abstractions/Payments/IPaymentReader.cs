using EarlyEagles.Application.Payments;

namespace EarlyEagles.Application.Abstractions.Payments;

/// <summary>
/// Read side for payments. Implementations read through the tenant query
/// filter, so every result belongs to the ambient tenant.
/// </summary>
public interface IPaymentReader
{
    /// <summary>The session's unsettled rows, in review order; <c>null</c> when the ambient tenant has no such session.</summary>
    Task<IReadOnlyList<UnsettledPaymentRow>?> ListUnsettledAsync(Guid sessionId, CancellationToken cancellationToken);
}
