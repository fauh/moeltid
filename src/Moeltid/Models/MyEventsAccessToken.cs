using System.ComponentModel.DataAnnotations;

namespace Moeltid.Models;

/// <summary>
/// Short-lived single-use token that grants access to the /my-events list page.
/// Token (PK) is a 32-char URL-safe random string; Email is the address the token was issued for.
/// </summary>
public class MyEventsAccessToken
{
    /// <summary>32-char URL-safe random string — used as the primary key and in the magic-link URL.</summary>
    [MaxLength(64)]
    public required string Token { get; set; }

    /// <summary>Lower-cased via EF value converter. The email address this token was issued for.</summary>
    [MaxLength(200)]
    public required string Email { get; set; }

    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set on first valid use. Null means the token has not yet been consumed.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
