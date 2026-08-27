using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EarlyEagles.Infrastructure.Persistence;

/// <summary>
/// Lets the EF Core CLI (<c>dotnet ef migrations</c> / <c>database update</c>)
/// construct an <see cref="ApplicationDbContext"/> without booting the web host.
/// The context requires an <see cref="Application.Abstractions.Tenancy.ITenantContext"/>;
/// design-time has no request, so an unresolved <see cref="TenantContext"/> is
/// supplied (migrations never run tenant-scoped queries).
/// </summary>
/// <remarks>
/// The connection string comes from the <c>ConnectionStrings__Postgres</c>
/// environment variable when set (e.g. pointing at a local/CI Postgres for
/// <c>database update</c>); otherwise it falls back to the same placeholder the
/// runtime uses, which is enough to scaffold migration SQL offline.
/// </remarks>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? DependencyInjection.DefaultConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options, new TenantContext());
    }
}
