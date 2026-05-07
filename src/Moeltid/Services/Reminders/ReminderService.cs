using Hangfire;
using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Models;

namespace Moeltid.Services.Reminders;

public class ReminderService(
    AppDbContext db,
    IBackgroundJobClient jobClient,
    ILogger<ReminderService> logger) : IReminderService
{
    public async Task<Reminder> ScheduleAsync(Guid eventId, DateTimeOffset whenUtc)
    {
        var existing = await db.Reminders.FindAsync(eventId);

        // Cancel the old Hangfire job if one is stored
        if (existing is not null && existing.HangfireJobId is not null)
        {
            jobClient.Delete(existing.HangfireJobId);
            logger.LogInformation(
                "Cancelled old Hangfire job {JobId} before rescheduling reminder for event {EventId}.",
                existing.HangfireJobId, eventId);
        }

        // Schedule the new job
        var delay = whenUtc - DateTimeOffset.UtcNow;
        var jobId = jobClient.Schedule<ReminderJob>(
            job => job.ExecuteAsync(eventId),
            delay > TimeSpan.Zero ? delay : TimeSpan.Zero);

        if (existing is null)
        {
            var reminder = new Reminder
            {
                EventId = eventId,
                ScheduledFor = whenUtc,
                IsSent = false,
                HangfireJobId = jobId,
            };
            db.Reminders.Add(reminder);
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Scheduled new reminder (job {JobId}) for event {EventId} at {When}.",
                jobId, eventId, whenUtc);
            return reminder;
        }
        else
        {
            existing.ScheduledFor = whenUtc;
            existing.IsSent = false;
            existing.HangfireJobId = jobId;
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Rescheduled reminder (job {JobId}) for event {EventId} at {When}.",
                jobId, eventId, whenUtc);
            return existing;
        }
    }

    public async Task CancelAsync(Guid eventId)
    {
        var reminder = await db.Reminders.FindAsync(eventId);
        if (reminder is null) return;

        if (reminder.HangfireJobId is not null)
        {
            jobClient.Delete(reminder.HangfireJobId);
            logger.LogInformation(
                "Cancelled Hangfire job {JobId} for event {EventId}.",
                reminder.HangfireJobId, eventId);
        }

        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync();
    }

    public Task<Reminder?> GetByEventAsync(Guid eventId) =>
        db.Reminders.FirstOrDefaultAsync(r => r.EventId == eventId);
}
