using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EarlyEagles.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> over Resend's REST API (<c>POST /emails</c>),
/// with a typed <see cref="HttpClient"/> rather than the Resend SDK: one
/// endpoint does not justify another dependency (P0-053.1).
/// </summary>
/// <remarks>
/// <para>The client's base address, bearer key and <c>User-Agent</c> are set
/// at registration. Resend refuses requests without a <c>User-Agent</c> (403,
/// error 1010), and <see cref="HttpClient"/> sends none by default.</para>
/// <para>Failures throw <see cref="EmailDeliveryException"/>: 429, 5xx, a
/// timeout or an unreachable host are transient; any other refusal is not.
/// Neither the log nor the exception carries addresses or bodies.</para>
/// </remarks>
public sealed class ResendEmailSender : IEmailSender
{
    /// <summary>Resend's per-email recipient limit.</summary>
    public const int MaxRecipients = 50;

    /// <summary>Resend's idempotency key length limit.</summary>
    public const int MaxIdempotencyKeyLength = 256;

    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient http;
    private readonly ResendOptions options;
    private readonly ILogger<ResendEmailSender> logger;

    public ResendEmailSender(HttpClient http, IOptions<ResendOptions> options, ILogger<ResendEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.http = http;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.To.Count is 0 or > MaxRecipients)
        {
            throw new ArgumentException($"An email needs 1 to {MaxRecipients} recipients.", nameof(message));
        }

        if (message.IdempotencyKey is { Length: 0 or > MaxIdempotencyKeyLength })
        {
            throw new ArgumentException($"An idempotency key must be 1 to {MaxIdempotencyKeyLength} characters.", nameof(message));
        }

        var body = new SendEmailRequest(options.From!, message.To, message.Subject, message.Html, message.Text, message.ReplyTo);
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(body, options: SerializerOptions),
        };
        if (message.IdempotencyKey is not null)
        {
            request.Headers.Add(IdempotencyKeyHeader, message.IdempotencyKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new EmailDeliveryException("Resend could not be reached.", statusCode: null, errorName: null, isTransient: true, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation.
            throw new EmailDeliveryException("Resend did not answer in time.", statusCode: null, errorName: null, isTransient: true, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await RefusedAsync(response, cancellationToken).ConfigureAwait(false);
            }

            var sent = await response.Content.ReadFromJsonAsync<SendEmailResponse>(SerializerOptions, cancellationToken).ConfigureAwait(false);
            ResendEmailSenderLog.Sent(logger, sent?.Id, message.To.Count);
            return sent?.Id;
        }
    }

    private static async Task<EmailDeliveryException> RefusedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        ResendError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ResendError>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // A proxy or gateway page rather than Resend's JSON; the status is enough.
        }

        var transient = response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500;
        var detail = string.IsNullOrWhiteSpace(error?.Message) ? string.Empty : $": {error.Message}";
        return new EmailDeliveryException(
            $"Resend refused the email ({status} {error?.Name ?? "unknown_error"}){detail}",
            status,
            error?.Name,
            transient);
    }

    private sealed record SendEmailRequest(string From, IReadOnlyList<string> To, string Subject, string Html, string? Text, string? ReplyTo);

    private sealed record SendEmailResponse(string? Id);

    private sealed record ResendError(string? Name, string? Message);
}
