using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Moeltid.Services.Email;

/// <summary>
/// Sends email via the Resend API (https://resend.com).
/// Registered as a typed HttpClient; Bearer auth set per-request from EmailSettings.ApiKey.
/// Throws HttpRequestException on non-2xx so callers can wrap in try/catch.
/// </summary>
public sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<EmailSettings> settings,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private static readonly Uri ResendSendUri = new("https://api.resend.com/emails");
    private readonly EmailSettings _settings = settings.Value;

    public async Task SendAsync(string to, string subject, string body)
    {
        var payload = new
        {
            from = _settings.FromAddress,
            to = new[] { to },
            subject,
            text = body,
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, ResendSendUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = content;

        logger.LogInformation("Sending email via Resend to {To} — subject: {Subject}", to, subject);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogWarning(
                "Resend API returned {StatusCode} for email to {To}. Body: {Error}",
                (int)response.StatusCode, to, error);
            response.EnsureSuccessStatusCode(); // throws HttpRequestException for caller to catch
        }
    }
}
