using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Application.Common;
using Mediator;

namespace EarlyEagles.Application.Payments;

public sealed class GetUnsettledPaymentsQueryHandler : IQueryHandler<GetUnsettledPaymentsQuery, IReadOnlyList<UnsettledPaymentRow>>
{
    private readonly IPaymentReader reader;

    public GetUnsettledPaymentsQueryHandler(IPaymentReader reader)
    {
        this.reader = reader;
    }

    public async ValueTask<IReadOnlyList<UnsettledPaymentRow>> Handle(GetUnsettledPaymentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await reader.ListUnsettledAsync(query.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Session not found.");
    }
}
