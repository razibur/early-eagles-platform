using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Tenancy;

/// <summary>EF <see cref="IClubClock"/>. The tenant query filter leaves only the ambient tenant's row.</summary>
public sealed class EfClubClock : IClubClock
{
    private readonly ApplicationDbContext context;
    private readonly TimeProvider timeProvider;

    public EfClubClock(ApplicationDbContext context, TimeProvider timeProvider)
    {
        this.context = context;
        this.timeProvider = timeProvider;
    }

    public async Task<DateTime> GetNowAsync(CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .AsNoTracking()
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return tenant.ToClubTime(timeProvider.GetUtcNow());
    }
}
