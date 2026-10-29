using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EarlyEagles.Infrastructure.Identity;

namespace EarlyEagles.Tests.Unit.Identity;

// P0-037 — the Svix signature scheme Clerk webhooks use. The first test is the
// example from Svix's own documentation and libraries, so it checks the
// implementation against the scheme's definition rather than against itself.
public class SvixWebhookVerifierTests
{
    private const string DocsSecret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw";
    private const string DocsMessageId = "msg_p5jXN8AQM9LWM0D4loKWxJek";
    private const string DocsTimestamp = "1614265330";
    private const string DocsBody = "{\"test\": 2432232314}";
    private const string DocsSignature = "v1,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=";

    private static readonly DateTimeOffset DocsSentAt = DateTimeOffset.FromUnixTimeSeconds(1614265330);

    [Fact]
    public void Verify_Accepts_The_Svix_Documentation_Example()
    {
        var verifier = new SvixWebhookVerifier(DocsSecret, new FixedTimeProvider(DocsSentAt));

        Assert.Equal(SvixVerificationResult.Valid, verifier.Verify(DocsMessageId, DocsTimestamp, DocsSignature, Body(DocsBody)));
    }

    [Fact]
    public void Verify_Accepts_A_Matching_Signature_Among_Several()
    {
        // During a secret rotation Svix sends one entry per active secret.
        var verifier = new SvixWebhookVerifier(DocsSecret, new FixedTimeProvider(DocsSentAt));
        var header = $"v1,{Convert.ToBase64String(new byte[32])} v2,ignored {DocsSignature}";

        Assert.Equal(SvixVerificationResult.Valid, verifier.Verify(DocsMessageId, DocsTimestamp, header, Body(DocsBody)));
    }

    [Fact]
    public void Verify_Rejects_A_Modified_Body()
    {
        var verifier = new SvixWebhookVerifier(DocsSecret, new FixedTimeProvider(DocsSentAt));

        Assert.Equal(
            SvixVerificationResult.InvalidSignature,
            verifier.Verify(DocsMessageId, DocsTimestamp, DocsSignature, Body("{\"test\": 2432232315}")));
    }

    [Fact]
    public void Verify_Rejects_A_Different_Message_Id()
    {
        var verifier = new SvixWebhookVerifier(DocsSecret, new FixedTimeProvider(DocsSentAt));

        Assert.Equal(
            SvixVerificationResult.InvalidSignature,
            verifier.Verify("msg_other", DocsTimestamp, DocsSignature, Body(DocsBody)));
    }

    [Fact]
    public void Verify_Rejects_A_Signature_Made_With_Another_Secret()
    {
        var verifier = new SvixWebhookVerifier(DocsSecret, new FixedTimeProvider(DocsSentAt));
        var otherKey = RandomNumberGenerator.GetBytes(24);
        var forged = Sign(otherKey, DocsMessageId, DocsTimestamp, DocsBody);

        Assert.Equal(SvixVerificationResult.InvalidSignature, verifier.Verify(DocsMessageId, DocsTimestamp, forged, Body(DocsBody)));
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v1,")]
    [InlineData("v1,not base64!")]
    [InlineData("g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=")]
    [InlineData("v0,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=")]
    public void Verify_Rejects_Malformed_Or_Unversioned_Signatures(string header)
    {
        var verifier = new SvixWebhookVerifier(DocsSecret, new FixedTimeProvider(DocsSentAt));

        Assert.Equal(SvixVerificationResult.InvalidSignature, verifier.Verify(DocsMessageId, DocsTimestamp, header, Body(DocsBody)));
    }

    [Theory]
    [InlineData(null, DocsTimestamp, DocsSignature)]
    [InlineData(DocsMessageId, null, DocsSignature)]
    [InlineData(DocsMessageId, DocsTimestamp, null)]
    [InlineData("", DocsTimestamp, DocsSignature)]
    public void Verify_Rejects_Missing_Headers(string? id, string? timestamp, string? signature)
    {
        var verifier = new SvixWebhookVerifier(DocsSecret, new FixedTimeProvider(DocsSentAt));

        Assert.Equal(SvixVerificationResult.MissingHeaders, verifier.Verify(id, timestamp, signature, Body(DocsBody)));
    }

    [Theory]
    [InlineData("-1614265330")]
    [InlineData("1614265330.5")]
    [InlineData("2021-02-25T15:02:10Z")]
    public void Verify_Rejects_A_Timestamp_That_Is_Not_Unix_Seconds(string timestamp)
    {
        var verifier = new SvixWebhookVerifier(DocsSecret, new FixedTimeProvider(DocsSentAt));

        Assert.Equal(SvixVerificationResult.InvalidTimestamp, verifier.Verify(DocsMessageId, timestamp, DocsSignature, Body(DocsBody)));
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void Verify_Rejects_A_Correctly_Signed_Message_Outside_Five_Minutes(int secondsFromNow)
    {
        // Signed correctly for its own timestamp, but too old (a replay) or too far ahead.
        var key = RandomNumberGenerator.GetBytes(24);
        var now = DocsSentAt;
        var timestamp = (now.ToUnixTimeSeconds() + secondsFromNow).ToString(CultureInfo.InvariantCulture);
        var verifier = new SvixWebhookVerifier(Secret(key), new FixedTimeProvider(now));

        Assert.Equal(
            SvixVerificationResult.TimestampOutOfTolerance,
            verifier.Verify(DocsMessageId, timestamp, Sign(key, DocsMessageId, timestamp, DocsBody), Body(DocsBody)));
    }

    [Theory]
    [InlineData(-300)]
    [InlineData(300)]
    public void Verify_Accepts_A_Message_At_The_Edge_Of_The_Tolerance(int secondsFromNow)
    {
        var key = RandomNumberGenerator.GetBytes(24);
        var now = DocsSentAt;
        var timestamp = (now.ToUnixTimeSeconds() + secondsFromNow).ToString(CultureInfo.InvariantCulture);
        var verifier = new SvixWebhookVerifier(Secret(key), new FixedTimeProvider(now));

        Assert.Equal(
            SvixVerificationResult.Valid,
            verifier.Verify(DocsMessageId, timestamp, Sign(key, DocsMessageId, timestamp, DocsBody), Body(DocsBody)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw")]
    [InlineData("whsec_")]
    [InlineData("whsec_not base64!")]
    public void Invalid_Secrets_Are_Reported_And_Rejected_By_The_Constructor(string? secret)
    {
        Assert.False(SvixWebhookVerifier.IsValidSecret(secret));
        Assert.Throws<ArgumentException>(() => new SvixWebhookVerifier(secret!, TimeProvider.System));
    }

    [Fact]
    public void A_Well_Formed_Secret_Is_Valid()
    {
        Assert.True(SvixWebhookVerifier.IsValidSecret(DocsSecret));
    }

    private static byte[] Body(string json) => Encoding.UTF8.GetBytes(json);

    private static string Secret(byte[] key) => SvixWebhookVerifier.SecretPrefix + Convert.ToBase64String(key);

    private static string Sign(byte[] key, string id, string timestamp, string body) =>
        "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}")));

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;

        public FixedTimeProvider(DateTimeOffset now) => this.now = now;

        public override DateTimeOffset GetUtcNow() => now;
    }
}
