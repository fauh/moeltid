using Moeltid.Models;

namespace Moeltid.Services.Reminders;

public interface IReminderService
{
    /// <summary>
    /// Schedules (or reschedules) a reminder for the given event.
    /// Creates the Reminder row if it doesn't exist; updates it if it does.
    /// Cancels any existing Hangfire job before scheduling the new one.
    /// </summary>
    Task<Reminder> ScheduleAsync(Guid eventId, DateTimeOffset whenUtc);

    /// <summary>
    /// Cancels and removes the pending reminder for the event, if one exists.
    /// No-op if no reminder exists or the reminder is already sent.
    /// </summary>
    Task CancelAsync(Guid eventId);

    /// <summary>Returns the reminder for the event, or null if none exists.</summary>
    Task<Reminder?> GetByEventAsync(Guid eventId);
}
