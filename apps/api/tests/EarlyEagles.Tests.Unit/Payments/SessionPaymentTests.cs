using EarlyEagles.Domain.Payments;

namespace EarlyEagles.Tests.Unit.Payments;

// P1-001.3 — Reported moves to Confirmed or Rejected; rejected can be reported
// again; confirmed is final.
public class SessionPaymentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    [Fact]
    public void Report_Starts_Reported_Trims_And_Raises_PaymentReported()
    {
        var payment = NewPayment(sender: "  A Rahman ", reference: " ZL123 ");

        Assert.Equal(PaymentStatus.Reported, payment.Status);
        Assert.Equal("A Rahman", payment.SenderName);
        Assert.Equal("ZL123", payment.Reference);
        Assert.Equal(1500, payment.AmountCents);
        var raised = Assert.IsType<PaymentReported>(Assert.Single(payment.DomainEvents));
        Assert.Equal(payment.Id, raised.PaymentId);
        Assert.Equal("user_1", raised.ClerkUserId);
    }

    [Theory]
    [InlineData("", "ref")]
    [InlineData("   ", "ref")]
    [InlineData("name", "")]
    [InlineData("name", "  ")]
    public void Report_Refuses_Blank_Text(string sender, string reference)
    {
        Assert.Throws<ArgumentException>(() => NewPayment(sender: sender, reference: reference));
    }

    [Fact]
    public void Report_Refuses_Text_Too_Long_And_A_Negative_Amount()
    {
        Assert.Throws<ArgumentException>(() => NewPayment(sender: new string('x', SessionPayment.SenderNameMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => NewPayment(reference: new string('x', SessionPayment.ReferenceMaxLength + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewPayment(amountCents: -1));
    }

    [Fact]
    public void Confirm_Records_The_Reviewer_And_Raises_PaymentConfirmed()
    {
        var payment = NewPayment();
        payment.ClearDomainEvents();

        Assert.True(payment.Confirm("user_admin", T0.AddHours(1)));

        Assert.Equal(PaymentStatus.Confirmed, payment.Status);
        Assert.Equal("user_admin", payment.ReviewedBy);
        Assert.Equal(T0.AddHours(1), payment.ReviewedAt);
        var raised = Assert.IsType<PaymentConfirmed>(Assert.Single(payment.DomainEvents));
        Assert.Equal(1500, raised.AmountCents);
    }

    [Fact]
    public void Confirming_Twice_Raises_One_Event()
    {
        var payment = NewPayment();
        payment.Confirm("user_admin", T0);
        payment.ClearDomainEvents();

        Assert.False(payment.Confirm("user_other", T0.AddHours(1)));

        Assert.Empty(payment.DomainEvents);
        Assert.Equal("user_admin", payment.ReviewedBy);
    }

    [Fact]
    public void Reject_Records_The_Reason_And_Raises_Nothing()
    {
        var payment = NewPayment();
        payment.ClearDomainEvents();

        Assert.True(payment.Reject("user_admin", " No such transfer ", T0));

        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal("No such transfer", payment.RejectionReason);
        Assert.Empty(payment.DomainEvents);
        Assert.False(payment.Reject("user_admin", "Again", T0));
        Assert.Equal("No such transfer", payment.RejectionReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_Needs_A_Reason_And_Leaves_The_Payment_Reported_Without_One(string reason)
    {
        var payment = NewPayment();

        Assert.Throws<ArgumentException>(() => payment.Reject("user_admin", reason, T0));

        Assert.Equal(PaymentStatus.Reported, payment.Status);
        Assert.Null(payment.ReviewedBy);
    }

    [Fact]
    public void A_Confirmed_Payment_Cant_Be_Rejected_Or_Reported_Again()
    {
        var payment = NewPayment();
        payment.Confirm("user_admin", T0);

        Assert.Throws<InvalidOperationException>(() => payment.Reject("user_admin", "Oops", T0));
        Assert.Throws<InvalidOperationException>(() => payment.ReportAgain(1500, "A", "B", T0));
    }

    [Fact]
    public void A_Rejected_Payment_Cant_Be_Confirmed_Until_Reported_Again()
    {
        var payment = NewPayment();
        payment.Reject("user_admin", "No such transfer", T0);

        Assert.Throws<InvalidOperationException>(() => payment.Confirm("user_admin", T0));
    }

    [Fact]
    public void A_Reported_Payment_Cant_Be_Reported_Again()
    {
        var payment = NewPayment();

        Assert.Throws<InvalidOperationException>(() => payment.ReportAgain(1500, "A", "B", T0));
    }

    [Fact]
    public void Reporting_Again_Clears_The_Verdict_And_Raises_PaymentReported()
    {
        var payment = NewPayment();
        payment.Reject("user_admin", "No such transfer", T0);
        payment.ClearDomainEvents();

        payment.ReportAgain(2000, "B Rahman", "ZL999", T0.AddDays(1));

        Assert.Equal(PaymentStatus.Reported, payment.Status);
        Assert.Equal("B Rahman", payment.SenderName);
        Assert.Equal("ZL999", payment.Reference);
        Assert.Equal(2000, payment.AmountCents);
        Assert.Equal(T0.AddDays(1), payment.ReportedAt);
        Assert.Null(payment.ReviewedBy);
        Assert.Null(payment.ReviewedAt);
        Assert.Null(payment.RejectionReason);
        Assert.IsType<PaymentReported>(Assert.Single(payment.DomainEvents));
    }

    private static SessionPayment NewPayment(string sender = "A Rahman", string reference = "ZL123", int amountCents = 1500) =>
        SessionPayment.Report(Guid.NewGuid(), Tenant, SessionId, "user_1", amountCents, sender, reference, T0);
}
