using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Payments;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Payments;

// P1-010 — a consumer has no ambient tenant, so the directory scopes every read
// itself: the admins of this club, and no other.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class PaymentEmailDirectoryTests : IAsyncLifetime
{
    private readonly PostgresFixture fixture;
    private Guid sessionId;
    private Guid paymentId;

    public PaymentEmailDirectoryTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);
        sessionId = await SessionApi.SeedSessionAsync(fixture, feeCents: 1800);

        await using var iota = SessionApi.Iota(fixture);
        if (!await iota.TenantMembers.AnyAsync(m => m.ClerkUserId == "user_email_admin"))
        {
            iota.TenantMembers.AddRange(
                Member(SessionApi.IotaTenantId, "user_email_admin", TenantMemberRole.TenantAdmin, "Admin", "admin@iota.example"),
                Member(SessionApi.IotaTenantId, "user_email_noaddress", TenantMemberRole.TenantAdmin, "No Address", null),
                Member(SessionApi.IotaTenantId, "user_email_member", TenantMemberRole.Member, "Karim", "karim@iota.example"));

            await using var kappa = SessionApi.Kappa(fixture);
            kappa.TenantMembers.Add(Member(SessionApi.KappaTenantId, "user_email_kappa", TenantMemberRole.TenantAdmin, "Other", "admin@kappa.example"));
            await kappa.SaveChangesAsync();
        }

        var payment = SessionPayment.Report(Guid.CreateVersion7(), SessionApi.IotaTenantId, sessionId, "user_email_member", 1800, "K Rahman", "ZL1", DateTimeOffset.UtcNow);
        iota.SessionPayments.Add(payment);
        await iota.SaveChangesAsync();
        paymentId = payment.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Finds_The_Facts_And_Only_This_Clubs_Admins_With_An_Address()
    {
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);

        var facts = await Directory(dataSource).FindAsync(SessionApi.IotaTenantId, paymentId, CancellationToken.None);

        Assert.NotNull(facts);
        Assert.Equal("Iota Club", facts!.ClubName);
        Assert.Contains("admin@iota.example", facts.AdminEmails);
        Assert.DoesNotContain("admin@kappa.example", facts.AdminEmails);
        Assert.DoesNotContain("karim@iota.example", facts.AdminEmails);
        Assert.Equal("Karim", facts.MemberName);
        Assert.Equal("Seeded Hall", facts.Venue);
        Assert.Equal(1800, facts.AmountCents);
        Assert.Equal("K Rahman", facts.SenderName);
    }

    [Fact]
    public async Task Another_Clubs_Id_Finds_Nothing()
    {
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);

        Assert.Null(await Directory(dataSource).FindAsync(SessionApi.KappaTenantId, paymentId, CancellationToken.None));
    }

    private static EfPaymentEmailDirectory Directory(NpgsqlDataSource dataSource) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(dataSource).Options);

    private static TenantMember Member(Guid tenantId, string userId, TenantMemberRole role, string name, string? email)
    {
        var member = TenantMember.Create(tenantId, userId, role);
        member.UpdateProfile(name, email, null);
        return member;
    }
}
