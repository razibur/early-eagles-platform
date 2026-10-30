using EarlyEagles.Infrastructure.Billing;
using Stripe;

namespace EarlyEagles.Tests.Unit.Billing;

// P0-044 — a Stripe delivery is believed only when it is signed with the
// endpoint's secret, recent, and an event in the SDK's API release train.
// Headers are made with Stripe.net's own GenerateSignatureHeader.
public class StripeWebhookVerifierTests
{
    private const string Secret = "whsec_unit_test_secret";

    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_Signed_Recent_Event_Is_Valid_And_Parsed()
    {
        var payload = Event("checkout.session.completed");

        var result = Verifier().Verify(payload, Sign(payload), out var stripeEvent);

        Assert.Equal(StripeWebhookVerificationResult.Valid, result);
        Assert.Equal("evt_unit_1", stripeEvent!.Id);
        Assert.Equal("checkout.session.completed", stripeEvent.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_Header_Is_Missing_Signature(string? header)
    {
        Assert.Equal(StripeWebhookVerificationResult.MissingSignature, Verifier().Verify(Event("invoice.paid"), header, out var stripeEvent));
        Assert.Null(stripeEvent);
    }

    [Fact]
    public void A_Body_Changed_After_Signing_Is_Invalid()
    {
        var payload = Event("invoice.paid");
        var header = Sign(payload);

        var result = Verifier().Verify(payload.Replace("invoice.paid", "invoice.payment_failed", StringComparison.Ordinal), header, out var stripeEvent);

        Assert.Equal(StripeWebhookVerificationResult.InvalidSignature, result);
        Assert.Null(stripeEvent);
    }

    [Fact]
    public void Another_Secret_Is_Invalid()
    {
        var payload = Event("invoice.paid");

        Assert.Equal(StripeWebhookVerificationResult.InvalidSignature, Verifier().Verify(payload, Sign(payload, secret: "whsec_someone_else"), out _));
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void A_Timestamp_More_Than_Five_Minutes_Away_Is_Invalid(int seconds)
    {
        var payload = Event("invoice.paid");

        Assert.Equal(StripeWebhookVerificationResult.InvalidSignature, Verifier().Verify(payload, Sign(payload, Now.AddSeconds(seconds)), out _));
    }

    [Theory]
    [InlineData(-299)]
    [InlineData(299)]
    public void A_Timestamp_Within_Five_Minutes_Is_Accepted(int seconds)
    {
        var payload = Event("invoice.paid");

        Assert.Equal(StripeWebhookVerificationResult.Valid, Verifier().Verify(payload, Sign(payload, Now.AddSeconds(seconds)), out _));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("t=abc,v1=def")]
    public void A_Malformed_Header_Is_Invalid(string header)
    {
        Assert.Equal(StripeWebhookVerificationResult.InvalidSignature, Verifier().Verify(Event("invoice.paid"), header, out _));
    }

    [Fact]
    public void A_Signed_Event_From_Another_Api_Release_Train_Is_A_Version_Mismatch()
    {
        var payload = Event("invoice.paid", apiVersion: "2020-08-27");

        Assert.Equal(StripeWebhookVerificationResult.ApiVersionMismatch, Verifier().Verify(payload, Sign(payload), out var stripeEvent));
        Assert.Null(stripeEvent);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"object":"event","id":"evt_1"}""")]
    [InlineData("""{"object":"event","id":"evt_1","type":"invoice.paid"}""")]
    public void A_Signed_Body_That_Is_Not_An_Event_Is_Malformed(string payload)
    {
        Assert.Equal(StripeWebhookVerificationResult.Malformed, Verifier().Verify(payload, Sign(payload), out var stripeEvent));
        Assert.Null(stripeEvent);
    }

    [Theory]
    [InlineData("whsec_abc", true)]
    [InlineData("whsec_", false)]
    [InlineData("sk_test_abc", false)]
    [InlineData("whsec_abc def", false)]
    public void Secret_Shape(string secret, bool valid) => Assert.Equal(valid, StripeOptions.IsValidWebhookSecret(secret));

    internal static string Event(string type, string? apiVersion = null) => $$$"""
        {"id":"evt_unit_1","object":"event","api_version":"{{{apiVersion ?? StripeConfiguration.ApiVersion}}}","created":1793361600,"livemode":false,"pending_webhooks":1,"type":"{{{type}}}","data":{"object":{"id":"obj_1","object":"invoice"}}
        """ + "}";

    private static string Sign(string payload, DateTimeOffset? at = null, string secret = Secret) =>
        EventUtility.GenerateSignatureHeader(payload, secret, (at ?? Now).ToUnixTimeSeconds());

    private static StripeWebhookVerifier Verifier() => new(Secret, new FixedTime(Now));

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset now;

        public FixedTime(DateTimeOffset now) => this.now = now;

        public override DateTimeOffset GetUtcNow() => now;
    }
}
