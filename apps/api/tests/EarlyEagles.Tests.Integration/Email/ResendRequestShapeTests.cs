using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using EarlyEagles.Application.Abstractions.Email;
using EarlyEagles.Infrastructure.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Tests.Integration.Email;

// P0-053.2 — the request the API's own IEmailSender sends Resend: the host's
// registration (base address, key, User-Agent) and the sender's body, with
// only the network swapped for a recording handler. No Resend account needed.
public sealed class ResendRequestShapeTests : IClassFixture<ResendRequestShapeTests.ResendFactory>
{
    private const string ApiKey = "re_shape_test_key";
    private const string From = "Early Eagles <no-reply@earlyeagles.club>";
    private const string EmailId = "4ef9a417-02e9-4d39-ad75-9611e0fcc33c";

    private readonly ResendFactory factory;

    public ResendRequestShapeTests(ResendFactory factory) => this.factory = factory;

    [Fact]
    public async Task A_Full_Message_Becomes_One_Authenticated_Post_With_Snake_Case_Json()
    {
        var message = new EmailMessage(
            ["Rahim <rahim@example.com>", "karim@example.com"],
            "Dues received",
            "<p>Thanks, Rahim.</p>",
            Text: "Thanks, Rahim.",
            ReplyTo: "treasurer@example.com",
            IdempotencyKey: "payment-confirmed/0f8fad5b-d9cb-469f-a165-70867728950e");

        var (id, request) = await SendAsync(message);

        Assert.Equal(EmailId, id);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://api.resend.com/emails"), request.Uri);
        Assert.Equal("Bearer " + ApiKey, request.Authorization);
        Assert.StartsWith("EarlyEagles/", request.UserAgent, StringComparison.Ordinal);
        Assert.Equal("payment-confirmed/0f8fad5b-d9cb-469f-a165-70867728950e", request.IdempotencyKey);
        Assert.Equal("application/json", request.ContentType);

        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal(From, root.GetProperty("from").GetString());
        Assert.Equal(["Rahim <rahim@example.com>", "karim@example.com"], root.GetProperty("to").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("Dues received", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Thanks, Rahim.</p>", root.GetProperty("html").GetString());
        Assert.Equal("Thanks, Rahim.", root.GetProperty("text").GetString());
        Assert.Equal("treasurer@example.com", root.GetProperty("reply_to").GetString());
        Assert.Equal(
            ["from", "to", "subject", "html", "text", "reply_to"],
            root.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Optional_Fields_And_The_Idempotency_Header_Are_Left_Out_When_Unset()
    {
        var (_, request) = await SendAsync(new EmailMessage(["karim@example.com"], "Hello", "<p>Hi</p>"));

        Assert.Null(request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(["from", "to", "subject", "html"], body.RootElement.EnumerateObject().Select(p => p.Name));
    }

    private async Task<(string? Id, RecordedRequest Request)> SendAsync(EmailMessage message)
    {
        factory.Requests.Clear();
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        Assert.IsType<ResendEmailSender>(sender);

        var id = await sender.SendAsync(message, CancellationToken.None);

        return (id, Assert.Single(factory.Requests));
    }

    public sealed record RecordedRequest(
        HttpMethod Method,
        Uri? Uri,
        string? Authorization,
        string UserAgent,
        string? IdempotencyKey,
        string? ContentType,
        string Body);

    public sealed class ResendFactory : WebApplicationFactory<Program>
    {
        public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Resend:ApiKey", ApiKey);
            builder.UseSetting("Resend:From", From);
            builder.ConfigureServices(services =>
                services.AddHttpClient<IEmailSender, ResendEmailSender>()
                    .ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(Requests)));
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<RecordedRequest> requests;

        public RecordingHandler(ConcurrentQueue<RecordedRequest> requests) => this.requests = requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Enqueue(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.ToString(),
                request.Headers.UserAgent.ToString(),
                request.Headers.TryGetValues(ResendEmailSender.IdempotencyKeyHeader, out var keys) ? keys.Single() : null,
                request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"id":"{{EmailId}}"}""", Encoding.UTF8, "application/json"),
            };
        }
    }
}
