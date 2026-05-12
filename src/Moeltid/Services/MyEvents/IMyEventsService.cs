using Moeltid.Models;

namespace Moeltid.Services.MyEvents;

/// <summary>
/// Manages the /my-events discovery flow:
/// 1. User requests access → receives a magic-link email.
/// 2. User clicks the magic link → token is validated + consumed → events list is rendered.
/// </summary>
public interface IMyEventsService
{
    /// <summary>
    /// Validates the email, generates a single-use token, persists it, and sends a magic-link
    /// email (best-effort, fire-and-forget). Always returns without indicating whether the
    /// email matched any events — closes the timing side-channel.
    /// </summary>
    Task RequestAccessAsync(string email);

    /// <summary>
    /// Finds the token, checks it is unexpired and unconsumed, marks it consumed, and returns
    /// the associated email. Returns <c>null</c> for any failure (missing, expired, or already
    /// used) — the caller renders the same generic "link expired or used" message regardless.
    /// </summary>
    Task<string?> ValidateAndConsumeAsync(string token);

    /// <summary>
    /// Returns the aggregated list of events (owner + attendee + invitee) for the given email,
    /// ordered ongoing-first then past-descending, with combined role badges and best-action URLs.
    /// </summary>
    Task<IReadOnlyList<MyEventRow>> GetEventsForEmailAsync(string email);
}

/// <summary>Roles the requesting user holds for a specific event (flags enum).</summary>
[Flags]
public enum EventRole
{
    None = 0,
    Owner = 1,
    Attendee = 2,
    Invitee = 4,
}

/// <summary>One row in the /my-events list page.</summary>
public record MyEventRow(
    Models.Event Event,
    EventRole Roles,
    /// <summary>Pre-resolved best-action URL (manage > attendee edit > invitee public).</summary>
    string ActionUrl,
    bool IsOngoing);
