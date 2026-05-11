namespace Moeltid.Services.Email;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>Absolute base URL prepended to all relative paths in email bodies, e.g. https://moeltid.se</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>From address used in outbound emails, e.g. no-reply@moeltid.se</summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>Resend API key. Set via user-secrets in dev; host env var EmailSettings__ApiKey in production.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>When true, use the real email provider. When false, use ConsoleEmailSender (default for Development).</summary>
    public bool UseRealProvider { get; init; } = false;
}
