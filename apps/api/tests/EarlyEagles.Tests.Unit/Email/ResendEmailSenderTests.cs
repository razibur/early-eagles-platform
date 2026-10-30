using System.Net;
using System.Text;
using EarlyEagles.Application.Abstractions.Email;
using EarlyEagles.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Unit.Email;

// P0-053.1 — what a Resend answer turns into. The request itself is checked in
// the integration suite (P0-053.2).
public class ResendEmailSenderTests
{
    private static readonly EmailMessage Message = new(["member@example.com"], "Dues received", "<p>Thanks</p>");

    [Fact]
    public async Task An_Accepted_Email_Returns_Resends_Id()
    {
        var sender = Sender(_ => Json(HttpStatusCode.OK, """{"id":"49a3999c-0ce1-4ea6-ab68-afcd6dc2e794"}"""));

        var id = await sender.SendAsync(Message, CancellationToken.None);

        Assert.Equal("49a3999c-0ce1-4ea6-ab68-afcd6dc2e794", id);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "validation_error", false)]
    [InlineData(HttpStatusCode.Forbidden, "invalid_from_address", false)]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_exceeded", true)]
    [InlineData(HttpStatusCode.InternalServerError, "internal_server_error", true)]
    public async Task A_Refusal_Throws_With_Status_Name_And_Whether_To_Retry(HttpStatusCode status, string name, bool transient)
    {
        var sender = Sender(_ => Json(status, $$"""{"statusCode":{{(int)status}},"name":"{{name}}","message":"nope"}"""));

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(Message, CancellationToken.None));

        Assert.Equal((int)status, ex.StatusCode);
        Assert.Equal(name, ex.ErrorName);
        Assert.Equal(transient, ex.IsTransient);
        Assert.Contains("nope", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("member@example.com", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Gateway_Page_Instead_Of_Json_Still_Throws_A_Transient_Failure()
    {
        var sender = Sender(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>502</html>", Encoding.UTF8, "text/html"),
        });

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(Message, CancellationToken.None));

        Assert.Equal(502, ex.StatusCode);
        Assert.Null(ex.ErrorName);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task An_Unreachable_Host_Is_Transient()
    {
        var sender = Sender(_ => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(Message, CancellationToken.None));

        Assert.Null(ex.StatusCode);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task A_Timeout_Is_Transient_But_The_Callers_Cancellation_Is_Not_Swallowed()
    {
        var timeout = Sender(_ => throw new TaskCanceledException("timeout"));
        Assert.True((await Assert.ThrowsAsync<EmailDeliveryException>(() => timeout.SendAsync(Message, CancellationToken.None))).IsTransient);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timeout.SendAsync(Message, cancelled.Token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ResendEmailSender.MaxRecipients + 1)]
    public async Task Recipient_Count_Outside_Resends_Limits_Is_Refused_Before_Sending(int count)
    {
        var sent = false;
        var sender = Sender(_ =>
        {
            sent = true;
            return Json(HttpStatusCode.OK, """{"id":"x"}""");
        });
        var to = Enumerable.Range(0, count).Select(i => $"m{i}@example.com").ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(Message with { To = to }, CancellationToken.None));
        Assert.False(sent);
    }

    [Fact]
    public async Task An_Overlong_Idempotency_Key_Is_Refused_Before_Sending()
    {
        var sender = Sender(_ => Json(HttpStatusCode.OK, """{"id":"x"}"""));
        var key = new string('k', ResendEmailSender.MaxIdempotencyKeyLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(Message with { IdempotencyKey = key }, CancellationToken.None));
    }

    private static ResendEmailSender Sender(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var http = new HttpClient(new StubHandler(respond)) { BaseAddress = ResendOptions.BaseAddress };
        var options = Options.Create(new ResendOptions { ApiKey = "re_test", From = "no-reply@earlyeagles.club" });
        return new ResendEmailSender(http, options, NullLogger<ResendEmailSender>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => this.respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
