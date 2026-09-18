using System.Reflection;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Auditing;
using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core context. Its defining responsibility is tenant
/// isolation: a global query filter is applied to every <see cref="ITenantScoped"/>
/// entity so that no query can see another tenant's rows. The filter compares each
/// row's tenant discriminator against <see cref="CurrentTenantId"/>, which is
/// snapshotted from <see cref="ITenantContext"/> when the (per-request) context is
/// constructed.
/// </summary>
/// <remarks>
/// <para>
/// When no tenant is resolved, <see cref="CurrentTenantId"/> is
/// <see cref="Guid.Empty"/>, so tenant-scoped queries match nothing rather than
/// leak data. Infrastructure that legitimately needs cross-tenant reads (e.g. the
/// pre-auth tenant-resolution lookup) must opt out explicitly with
/// <c>IgnoreQueryFilters()</c>.
/// </para>
/// <para>
/// The read side is only half of isolation: <see cref="TenantStampingInterceptor"/>,
/// registered in <see cref="OnConfiguring"/>, stamps and validates
/// <see cref="ITenantScoped.TenantId"/> on the way in so a write cannot land in
/// another tenant either.
/// </para>
/// </remarks>
public sealed class ApplicationDbContext : DbContext
{
    // Stateless, so one shared instance serves every context and every tenant —
    // the ambient tenant is read from the context being saved. Registering a new
    // instance per context would also bloat EF's internal service-provider cache.
    private static readonly TenantStampingInterceptor TenantStamping = new();

    private static readonly MethodInfo ApplyTenantFilterMethod = typeof(ApplicationDbContext)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        CurrentTenantId = tenantContext.IsResolved ? tenantContext.TenantId : Guid.Empty;
    }

    /// <summary>
    /// Tenant the current request operates on. The global query filter references
    /// this member (rather than capturing a value) so EF re-evaluates it per query
    /// against the executing context, keeping the cached model tenant-agnostic.
    /// </summary>
    public Guid CurrentTenantId { get; }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Attaches the write-side tenant guard. Done here rather than at each
    /// registration site because EF calls <c>OnConfiguring</c> however the context
    /// was built — DI, the design-time factory, or a test <c>new</c>-ing it — so
    /// no caller can construct a context without the guard.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.AddInterceptors(TenantStamping);

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Apply the tenant filter to every ITenantScoped entity. For the Tenant
        // aggregate the discriminator is its own primary key (a tenant is scoped to
        // itself); every other entity carries a dedicated TenantId column.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var tenantColumn = entityType.FindProperty(nameof(ITenantScoped.TenantId)) is not null
                ? nameof(ITenantScoped.TenantId)
                : entityType.FindPrimaryKey()!.Properties.Single().Name;

            ApplyTenantFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder, tenantColumn]);
        }

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder, string tenantColumn)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => EF.Property<Guid>(e, tenantColumn) == CurrentTenantId);
    }
}
