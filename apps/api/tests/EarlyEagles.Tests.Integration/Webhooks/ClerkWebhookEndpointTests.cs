using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using EarlyEagles.Api.Webhooks;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Webhooks;

// P0-036.1 / P0-037 — the Clerk webhook endpoint accepts a Svix-signed raw body
// and nothing else; P0-036.2.3 — organization.created provisions a tenant;
// P0-036.3.2 — organizationMembership.created adds a tenant member;
// P0-036.4.2 — it seeds the member's profile, and user.created stores nothing;
// P0-036.5.2 — user.updated patches the profile in every club the user is in.
// Requests are signed here exactly as Svix signs them. The signature tests use
// an event type with no handler, so they don't depend on what handlers write.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ClerkWebhookEndpointTests
{
    private const string OrganizationCreated = """{"type":"session.created","object":"event","data":{"id":"sess_29w9IfBrPmcpi0IeBVaKtA7R94W"}}""";

    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(24);
    private static readonly string Secret = "whsec_" + Convert.ToBase64String(Key);

    private readonly PostgresFixture fixture;

    public ClerkWebhookEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_Correctly_Signed_Event_Is_Accepted()
    {
        using var factory = new ClerkFactory(fixture, Secret);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationCreated));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_Body_Changed_After_Signing_Is_Rejected()
    {
        using var factory = new ClerkFactory(fixture, Secret);
        var request = Signed(OrganizationCreated);
        request.Content = Json(OrganizationCreated.Replace("sess_", "sess_x", StringComparison.Ordinal));

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid webhook signature", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Unsigned_Request_Is_Rejected()
    {
        using var factory = new ClerkFactory(fixture, Secret);
        using var request = new HttpRequestMessage(HttpMethod.Post, ClerkWebhookEndpoints.Route) { Content = Json(OrganizationCreated) };

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Replayed_Request_Older_Than_Five_Minutes_Is_Rejected()
    {
        using var factory = new ClerkFactory(fixture, Secret);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationCreated, DateTimeOffset.UtcNow.AddMinutes(-6)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Signed_Body_Without_An_Event_Type_Is_Rejected()
    {
        using var factory = new ClerkFactory(fixture, Secret);

        var response = await factory.CreateClient().SendAsync(Signed("""{"data":{}}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Malformed webhook payload", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Signed_Body_That_Is_Not_Json_Is_Rejected()
    {
        using var factory = new ClerkFactory(fixture, Secret);

        var response = await factory.CreateClient().SendAsync(Signed("not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_Oversized_Body_Is_Refused_Before_Verification()
    {
        using var factory = new ClerkFactory(fixture, Secret);
        var huge = "{\"type\":\"x\",\"pad\":\"" + new string('a', ClerkWebhookEndpoints.MaxBodyBytes) + "\"}";

        var response = await factory.CreateClient().SendAsync(Signed(huge));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Signing_Secret_The_Endpoint_Answers_503()
    {
        using var factory = new ClerkFactory(fixture, secret: null);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationCreated));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task In_Production_The_Bare_Host_Is_Not_Rejected_For_A_Missing_Tenant()
    {
        // Production returns 400 "Tenant not specified" for tenant-scoped paths on
        // the bare host; Clerk calls the bare host, so the webhook path is exempt.
        using var factory = new ClerkFactory(fixture, Secret, environment: "Production");

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationCreated));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void A_Malformed_Signing_Secret_Stops_The_Host_At_Startup()
    {
        using var factory = new ClerkFactory(fixture, "not-a-whsec-secret");

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("WebhookSigningSecret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_Created_Provisions_A_Tenant_Linked_To_The_Organization()
    {
        var (orgId, slug) = NewOrganization();
        using var factory = new ClerkFactory(fixture, Secret);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationEvent(orgId, slug, "Webhook Club")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tenant = await FindTenantAsync(orgId);
        Assert.NotNull(tenant);
        Assert.Equal(slug, tenant!.Slug);
        Assert.Equal("Webhook Club", tenant.DisplayName);
    }

    [Fact]
    public async Task A_Redelivered_Organization_Created_Leaves_One_Tenant()
    {
        var (orgId, slug) = NewOrganization();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Twice Club")))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Twice Club")))).StatusCode);

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(1, await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.ClerkOrganizationId == orgId));
    }

    [Theory]
    [InlineData("Early_Eagles")]
    [InlineData("-bad-start")]
    public async Task An_Unusable_Slug_Is_Acknowledged_Without_Creating_A_Tenant(string slug)
    {
        // Retrying can't fix the slug, so the delivery is acknowledged (and
        // logged as an error) rather than retried for days.
        var (orgId, _) = NewOrganization();
        using var factory = new ClerkFactory(fixture, Secret);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationEvent(orgId, slug, "Bad Slug Club")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await FindTenantAsync(orgId));
    }

    [Fact]
    public async Task A_Slug_Owned_By_Another_Tenant_Is_Not_Taken_Over()
    {
        var (orgId, _) = NewOrganization();
        using var factory = new ClerkFactory(fixture, Secret);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationEvent(orgId, PostgresFixture.AlphaSlug, "Impostor")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await FindTenantAsync(orgId));
        await using var db = fixture.CreateContext(new TenantContext());
        var alpha = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == PostgresFixture.AlphaTenantId);
        Assert.NotEqual("Impostor", alpha.DisplayName);
    }

    [Fact]
    public async Task Organization_Created_Without_A_Slug_Is_Acknowledged_Without_Creating_A_Tenant()
    {
        var (orgId, _) = NewOrganization();
        using var factory = new ClerkFactory(fixture, Secret);
        var body = $$$"""{"type":"organization.created","data":{"id":"{{{orgId}}}","name":"No Slug"}}""";

        var response = await factory.CreateClient().SendAsync(Signed(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await FindTenantAsync(orgId));
    }

    [Theory]
    [InlineData("org:admin", TenantMemberRole.TenantAdmin)]
    [InlineData("org:member", TenantMemberRole.Member)]
    [InlineData("org:custom_role", TenantMemberRole.Member)]
    public async Task Membership_Created_Adds_The_User_To_The_Organizations_Tenant(string clerkRole, TenantMemberRole expected)
    {
        var (orgId, slug) = NewOrganization();
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Member Club")));

        var response = await client.SendAsync(Signed(MembershipEvent(orgId, userId, clerkRole)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tenant = await FindTenantAsync(orgId);
        var member = await FindMemberAsync(userId);
        Assert.NotNull(member);
        Assert.Equal(tenant!.Id, member!.TenantId);
        Assert.Equal(expected, member.Role);
    }

    [Fact]
    public async Task A_Redelivered_Membership_Leaves_One_Member()
    {
        var (orgId, slug) = NewOrganization();
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Redelivery Club")));

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(MembershipEvent(orgId, userId, "org:member")))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(MembershipEvent(orgId, userId, "org:member")))).StatusCode);

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(1, await db.TenantMembers.IgnoreQueryFilters().CountAsync(m => m.ClerkUserId == userId));
    }

    [Fact]
    public async Task A_Membership_Arriving_Before_Its_Organization_Is_Retried_And_Then_Succeeds()
    {
        // Svix doesn't guarantee order: the creator's membership can land first.
        var (orgId, slug) = NewOrganization();
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();

        var early = await client.SendAsync(Signed(MembershipEvent(orgId, userId, "org:admin")));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, early.StatusCode);
        Assert.Null(await FindMemberAsync(userId));

        await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Late Club")));
        var retry = await client.SendAsync(Signed(MembershipEvent(orgId, userId, "org:admin")));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(TenantMemberRole.TenantAdmin, (await FindMemberAsync(userId))!.Role);
    }

    [Fact]
    public async Task A_Membership_Without_A_User_Id_Is_Acknowledged_Without_A_Member()
    {
        var (orgId, slug) = NewOrganization();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "No User Club")));
        var body = $$$$"""{"type":"organizationMembership.created","data":{"role":"org:member","organization":{"id":"{{{{orgId}}}}"},"public_user_data":{}}}""";

        var response = await client.SendAsync(Signed(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tenant = await FindTenantAsync(orgId);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.TenantMembers.IgnoreQueryFilters().AnyAsync(m => m.TenantId == tenant!.Id));
    }

    [Fact]
    public async Task A_Membership_Never_Touches_Another_Tenants_Members()
    {
        // Alpha's admin joins a new organization as a plain member: a new row in
        // the new tenant, Alpha's row unchanged.
        var (orgId, slug) = NewOrganization();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Second Club")));

        var response = await client.SendAsync(Signed(MembershipEvent(orgId, PostgresFixture.AlphaAdminUserId, "org:member")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        var alpha = await db.TenantMembers.IgnoreQueryFilters()
            .SingleAsync(m => m.TenantId == PostgresFixture.AlphaTenantId && m.ClerkUserId == PostgresFixture.AlphaAdminUserId);
        Assert.Equal(TenantMemberRole.TenantAdmin, alpha.Role);
        var tenant = await FindTenantAsync(orgId);
        var joined = await db.TenantMembers.IgnoreQueryFilters()
            .SingleAsync(m => m.TenantId == tenant!.Id && m.ClerkUserId == PostgresFixture.AlphaAdminUserId);
        Assert.Equal(TenantMemberRole.Member, joined.Role);
    }

    [Fact]
    public async Task Membership_Created_Seeds_The_Members_Profile()
    {
        var (orgId, slug) = NewOrganization();
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Profile Club")));
        var body = $$$$"""{"type":"organizationMembership.created","data":{"role":"org:member","organization":{"id":"{{{{orgId}}}}"},"public_user_data":{"user_id":"{{{{userId}}}}","first_name":" Rahim ","last_name":"Uddin","identifier":"rahim@example.com","image_url":"https://img.clerk.com/rahim"}}}""";

        var response = await client.SendAsync(Signed(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = await FindMemberAsync(userId);
        Assert.Equal("Rahim Uddin", member!.DisplayName);
        Assert.Equal("rahim@example.com", member.Email);
        Assert.Equal("https://img.clerk.com/rahim", member.ImageUrl);
    }

    [Fact]
    public async Task A_Phone_Only_Member_Gets_A_Profile_Without_An_Email()
    {
        // A phone-OTP user's identifier is their phone number, not an email.
        var (orgId, slug) = NewOrganization();
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Phone Club")));
        var body = $$$$"""{"type":"organizationMembership.created","data":{"role":"org:member","organization":{"id":"{{{{orgId}}}}"},"public_user_data":{"user_id":"{{{{userId}}}}","first_name":"Karim","last_name":null,"identifier":"+15555550100"}}}""";

        var response = await client.SendAsync(Signed(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = await FindMemberAsync(userId);
        Assert.Equal("Karim", member!.DisplayName);
        Assert.Null(member.Email);
        Assert.Null(member.ImageUrl);
    }

    [Fact]
    public async Task User_Created_Is_Acknowledged_And_Stores_Nothing()
    {
        // A new user is in no club yet; the membership event creates the member.
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        var body = $$$"""{"type":"user.created","object":"event","data":{"id":"{{{userId}}}","object":"user","first_name":"New","last_name":"User"}}""";

        var response = await client.SendAsync(Signed(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await FindMemberAsync(userId));
    }

    [Fact]
    public async Task User_Updated_Patches_The_Profile_In_Every_Club_And_Audits_Each_In_Its_Own_Tenant()
    {
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        var first = await JoinNewClubAsync(client, userId, "org:admin");
        var second = await JoinNewClubAsync(client, userId, "org:member");

        var response = await client.SendAsync(Signed(UserUpdatedEvent(userId, "Rahim", "Uddin", "rahim@new.example.com", "https://img.clerk.com/new")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        var members = await db.TenantMembers.IgnoreQueryFilters().Where(m => m.ClerkUserId == userId).ToListAsync();
        Assert.Equal(2, members.Count);
        Assert.All(members, m =>
        {
            Assert.Equal("Rahim Uddin", m.DisplayName);
            Assert.Equal("rahim@new.example.com", m.Email);
            Assert.Equal("https://img.clerk.com/new", m.ImageUrl);
        });
        Assert.Equal(TenantMemberRole.TenantAdmin, members.Single(m => m.TenantId == first).Role);

        var audited = await db.AuditEvents.IgnoreQueryFilters()
            .Where(e => e.Action == "UpdateTenantMemberProfileCommand" && (e.TenantId == first || e.TenantId == second))
            .Select(e => e.TenantId)
            .ToListAsync();
        Assert.Equal(new[] { first, second }.Order(), audited.Order());
    }

    [Fact]
    public async Task User_Updated_Without_A_Primary_Email_Clears_The_Email()
    {
        // A user who removed their email and signs in by phone.
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();
        await JoinNewClubAsync(client, userId, "org:member");
        var body = $$$$"""{"type":"user.updated","data":{"id":"{{{{userId}}}}","first_name":"Karim","primary_email_address_id":null,"email_addresses":[],"phone_numbers":[{"id":"idn_1","phone_number":"+15555550100"}]}}""";

        var response = await client.SendAsync(Signed(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = await FindMemberAsync(userId);
        Assert.Equal("Karim", member!.DisplayName);
        Assert.Null(member.Email);
    }

    [Fact]
    public async Task User_Updated_For_A_User_In_No_Club_Is_Acknowledged()
    {
        var userId = NewUserId();
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();

        var response = await client.SendAsync(Signed(UserUpdatedEvent(userId, "Lone", "User", "lone@example.com", null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await FindMemberAsync(userId));
    }

    [Fact]
    public async Task User_Updated_Without_An_Id_Is_Acknowledged_And_Changes_Nothing()
    {
        using var factory = new ClerkFactory(fixture, Secret);
        var client = factory.CreateClient();

        var response = await client.SendAsync(Signed("""{"type":"user.updated","data":{"first_name":"Nobody"}}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.TenantMembers.IgnoreQueryFilters().AnyAsync(m => m.DisplayName == "Nobody"));
    }

    private static string UserUpdatedEvent(string userId, string firstName, string lastName, string email, string? imageUrl) =>
        $$$$"""{"type":"user.updated","object":"event","data":{"id":"{{{{userId}}}}","object":"user","first_name":"{{{{firstName}}}}","last_name":"{{{{lastName}}}}","image_url":{{{{(imageUrl is null ? "null" : $"\"{imageUrl}\"")}}}},"primary_email_address_id":"idn_primary","email_addresses":[{"id":"idn_other","email_address":"other@example.com"},{"id":"idn_primary","email_address":"{{{{email}}}}"}]}}""";

    private static (string OrgId, string Slug) NewOrganization()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return ("org_" + suffix, "hook-" + suffix);
    }

    private static string NewUserId() => "user_" + Guid.NewGuid().ToString("N")[..12];

    private static string MembershipEvent(string orgId, string userId, string role) =>
        $$$$"""{"type":"organizationMembership.created","object":"event","data":{"id":"orgmem_{{{{Guid.NewGuid():N}}}}","object":"organization_membership","role":"{{{{role}}}}","organization":{"id":"{{{{orgId}}}}","object":"organization"},"public_user_data":{"user_id":"{{{{userId}}}}","identifier":"someone@example.com"}}}""";

    private static string OrganizationEvent(string orgId, string slug, string name) =>
        $$$"""{"type":"organization.created","object":"event","data":{"id":"{{{orgId}}}","slug":"{{{slug}}}","name":"{{{name}}}","object":"organization"}}""";

    private static HttpRequestMessage Signed(string body, DateTimeOffset? sentAt = null)
    {
        var id = "msg_" + Guid.NewGuid().ToString("N");
        var timestamp = (sentAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Convert.ToBase64String(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}")));

        var request = new HttpRequestMessage(HttpMethod.Post, ClerkWebhookEndpoints.Route) { Content = Json(body) };
        request.Headers.Add("svix-id", id);
        request.Headers.Add("svix-timestamp", timestamp);
        request.Headers.Add("svix-signature", "v1," + signature);
        return request;
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private async Task<Guid> JoinNewClubAsync(HttpClient client, string userId, string role)
    {
        var (orgId, slug) = NewOrganization();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(OrganizationEvent(orgId, slug, "Profile Club")))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(MembershipEvent(orgId, userId, role)))).StatusCode);
        return (await FindTenantAsync(orgId))!.Id;
    }

    private async Task<Tenant?> FindTenantAsync(string orgId)
    {
        await using var db = fixture.CreateContext(new TenantContext());
        return await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.ClerkOrganizationId == orgId);
    }

    private async Task<TenantMember?> FindMemberAsync(string userId)
    {
        await using var db = fixture.CreateContext(new TenantContext());
        return await db.TenantMembers.IgnoreQueryFilters().SingleOrDefaultAsync(m => m.ClerkUserId == userId);
    }

    private sealed class ClerkFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly string? secret;
        private readonly string environment;

        public ClerkFactory(PostgresFixture fixture, string? secret, string environment = "Development")
        {
            connectionString = fixture.ConnectionString;
            this.secret = secret;
            this.environment = environment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Clerk:WebhookSigningSecret", secret ?? string.Empty);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
