using Moeltid.Models;

namespace Moeltid.Services.Reminders;

/// <summary>
/// No-op implementation of <see cref="IReminderService"/> used when
/// <c>Reminders:Enabled = false</c> (e.g. Render free tier where Hangfire
/// cannot run reliably). The reminder UI is hidden when this service is active,
/// so <see cref="ScheduleAsync"/> should never be called in practice.
/// </summary>
public sealed class NullReminderService : IReminderService
{
    public Task<Reminder> ScheduleAsync(Guid eventId, DateTimeOffset whenUtc) =>
        throw new NotSupportedException(
            "Reminders are disabled in this environment (Reminders:Enabled = false). " +
            "The reminder UI should be hidden when NullReminderService is registered.");

    public Task CancelAsync(Guid eventId) => Task.CompletedTask;

    public Task<Reminder?> GetByEventAsync(Guid eventId) =>
        Task.FromResult<Reminder?>(null);
}
