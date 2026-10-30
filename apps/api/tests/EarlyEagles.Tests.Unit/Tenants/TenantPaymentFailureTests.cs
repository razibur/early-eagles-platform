using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-043.4 — a failed invoice payment moves an active tenant to past due and
// raises PaymentFailed once; Stripe's retries and redeliveries change nothing.
public class TenantPaymentFailureTests
{
    private const string Invoice = "in_1MtHbELkdIwHu7ixl4OzzPMv";

    [Fact]
    public void An_Active_Tenant_Goes_Past_Due_And_Raises_Payment_Failed()
    {
        var tenant = NewTenant();

        Assert.True(tenant.RecordPaymentFailure(Invoice));

        Assert.Equal(TenantStatus.PastDue, tenant.Status);
        var raised = Assert.IsType<PaymentFailed>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(tenant.Id, raised.TenantId);
        Assert.Equal(Invoice, raised.StripeInvoiceId);
    }

    [Fact]
    public void A_Tenant_Already_Past_Due_Raises_Nothing_More()
    {
        var tenant = NewTenant();
        tenant.RecordPaymentFailure(Invoice);

        Assert.False(tenant.RecordPaymentFailure(Invoice));
        Assert.False(tenant.RecordPaymentFailure("in_another_attempt"));

        Assert.Single(tenant.DomainEvents);
        Assert.Equal(TenantStatus.PastDue, tenant.Status);
    }

    [Fact]
    public void A_Suspended_Tenant_Is_Refused()
    {
        var tenant = NewTenant();
        tenant.Suspend();

        Assert.Throws<InvalidOperationException>(() => tenant.RecordPaymentFailure(Invoice));
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Empty(tenant.DomainEvents);
    }

    [Fact]
    public void A_Deactivated_Tenant_Is_Refused()
    {
        var tenant = NewTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.RecordPaymentFailure(Invoice));
        Assert.Empty(tenant.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_Blank_Invoice_Id_Is_Refused(string invoiceId)
    {
        var tenant = NewTenant();

        Assert.Throws<ArgumentException>(() => tenant.RecordPaymentFailure(invoiceId));
        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void An_Invoice_Id_Longer_Than_A_Stripe_Id_Is_Refused()
    {
        Assert.Throws<ArgumentException>(() => NewTenant().RecordPaymentFailure("in_" + new string('x', Tenant.StripeIdMaxLength)));
    }

    private static Tenant NewTenant() => Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");
}
