using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Abstractions.Tenancy;
using Mediator;

namespace EarlyEagles.Application.Sessions;

public sealed class GetLeaderboardQueryHandler : IQueryHandler<GetLeaderboardQuery, IReadOnlyList<LeaderboardRow>>
{
    private readonly ISessionReader reader;
    private readonly IClubClock clubClock;

    public GetLeaderboardQueryHandler(ISessionReader reader, IClubClock clubClock)
    {
        this.reader = reader;
        this.clubClock = clubClock;
    }

    public async ValueTask<IReadOnlyList<LeaderboardRow>> Handle(GetLeaderboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clubNow = await clubClock.GetNowAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ListLeaderboardAsync(clubNow, cancellationToken).ConfigureAwait(false);
    }
}
