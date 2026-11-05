using EarlyEagles.Application.Abstractions.Email;
using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Application.Payments;
using EarlyEagles.Domain.Payments;

namespace EarlyEagles.Tests.Unit.Payments;

// P1-010 — what the admins receive, and when nothing goes out.
public class PaymentConfirmedNotifierTests
{
    private static readonly PaymentConfirmed Confirmed = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "user_1", 1500);

    [Fact]
    public async Task Sends_One_Email_To_Every_Admin_With_The_Facts()
    {
        var sender = new RecordingSender();
        var facts = new PaymentEmailFacts("Early Eagles", ["a@example.com", "b@example.com"], "Karim", new DateOnly(2026, 11, 8), "Edison Hall", 1850, "K Rahman");

        await new PaymentConfirmedNotifier(new FixedDirectory(facts), sender).NotifyAsync(Confirmed, "msg-1", CancellationToken.None);

        var email = Assert.Single(sender.Sent);
        Assert.Equal(["a@example.com", "b@example.com"], email.To);
        Assert.Equal("Early Eagles: payment confirmed for Karim", email.Subject);
        Assert.Equal("Karim paid $18.50 for Sunday, November 8, 2026 at Edison Hall. The Zelle transfer was from K Rahman.", email.Text);
        Assert.Equal("payment-confirmed/msg-1", email.IdempotencyKey);
    }

    [Fact]
    public async Task Text_A_Member_Typed_Is_Encoded_In_The_Html()
    {
        var sender = new RecordingSender();
        var facts = new PaymentEmailFacts("Club", ["a@example.com"], "Karim", new DateOnly(2026, 11, 8), "Hall", 1500, "<img src=x>");

        await new PaymentConfirmedNotifier(new FixedDirectory(facts), sender).NotifyAsync(Confirmed, "msg-1", CancellationToken.None);

        var html = Assert.Single(sender.Sent).Html;
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Member_With_No_Name_Reads_As_A_Member()
    {
        var sender = new RecordingSender();
        var facts = new PaymentEmailFacts("Club", ["a@example.com"], null, new DateOnly(2026, 11, 8), "Hall", 1500, "Sender");

        await new PaymentConfirmedNotifier(new FixedDirectory(facts), sender).NotifyAsync(Confirmed, "msg-1", CancellationToken.None);

        Assert.StartsWith("A member paid", Assert.Single(sender.Sent).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_Admin_With_An_Email_Sends_Nothing()
    {
        var sender = new RecordingSender();
        var facts = new PaymentEmailFacts("Club", [], "Karim", new DateOnly(2026, 11, 8), "Hall", 1500, "Sender");

        await new PaymentConfirmedNotifier(new FixedDirectory(facts), sender).NotifyAsync(Confirmed, "msg-1", CancellationToken.None);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task An_Unknown_Payment_Sends_Nothing()
    {
        var sender = new RecordingSender();

        await new PaymentConfirmedNotifier(new FixedDirectory(null), sender).NotifyAsync(Confirmed, "msg-1", CancellationToken.None);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task A_Delivery_Failure_Propagates_So_The_Bus_Retries()
    {
        var facts = new PaymentEmailFacts("Club", ["a@example.com"], "Karim", new DateOnly(2026, 11, 8), "Hall", 1500, "Sender");
        var notifier = new PaymentConfirmedNotifier(new FixedDirectory(facts), new FailingSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => notifier.NotifyAsync(Confirmed, "msg-1", CancellationToken.None));
    }

    private sealed class FixedDirectory(PaymentEmailFacts? facts) : IPaymentEmailDirectory
    {
        public Task<PaymentEmailFacts?> FindAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken) => Task.FromResult(facts);
    }

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FailingSender : IEmailSender
    {
        public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Provider down.");
    }
}
