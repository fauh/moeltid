using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moeltid.Data;
using Moeltid.Models;
using Moeltid.Services.Email;

namespace Moeltid.Services.Reminders;

/// <summary>
/// Hangfire job that fires a scheduled reminder for an event.
/// Loads the audience, builds per-recipient bodies, sends emails best-effort.
/// One failure doesn't abort the batch.
/// </summary>
public class ReminderJob(
    AppDbContext db,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
    ILogger<ReminderJob> logger)
{
    private readonly EmailSettings _emailSettings = emailSettings.Value;

    public async Task ExecuteAsync(Guid eventId)
    {
        var ev = await db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null)
        {
            logger.LogWarning("ReminderJob: event {EventId} not found; skipping.", eventId);
            return;
        }

        var reminder = await db.Reminders.FindAsync(eventId);
        if (reminder is null)
        {
            logger.LogWarning("ReminderJob: no Reminder row for event {EventId}; skipping.", eventId);
            return;
        }

        if (reminder.IsSent)
        {
            logger.LogInformation("ReminderJob: reminder for event {EventId} already marked sent; skipping.", eventId);
            return;
        }

        // Load attendances (with MealOption for order text) and all invitees
        var attendances = await db.Attendances
            .Where(a => a.EventId == eventId)
            .Include(a => a.MealOption)
            .ToListAsync();

        var invitees = await db.Invitees
            .Where(i => i.EventId == eventId)
            .ToListAsync();

        var audience = ReminderAudience.Build(attendances, invitees);
        var deadlineLocal = TimeZoneHelper.ToLocalString(ev.Deadline, ev.TimeZoneId, "yyyy-MM-dd HH:mm");

        var sent = 0;
        foreach (var recipient in audience)
        {
            var (subject, body) = recipient.Kind switch
            {
                RecipientKind.HasOrdered => BuildOrderedBody(ev, recipient),
                RecipientKind.NotOrdered => BuildNotOrderedBody(ev, recipient, deadlineLocal),
                _ => (null, null),
            };

            if (subject is null || body is null) continue;

            try
            {
                await emailSender.SendAsync(recipient.Email, subject, body);
                sent++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "ReminderJob: failed to send to {Email} for event {EventId}.",
                    recipient.Email, eventId);
            }
        }

        // Mark sent even if some individual sends failed; best-effort design
        reminder.IsSent = true;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "ReminderJob: sent {Sent}/{Total} reminder emails for event {EventId}.",
            sent, audience.Count, eventId);
    }

    private (string subject, string body) BuildOrderedBody(Event ev, RecipientLine recipient)
    {
        var editUrl = $"{_emailSettings.BaseUrl}/e/{ev.Slug}";
        var subject = $"Reminder: your order for \"{ev.Title}\"";
        var body = $"""
            Hi,

            Just a reminder — you're confirmed for "{ev.Title}".

            Your order: {recipient.OrderText}

            If you need to update your order, visit:
            {editUrl}
            """;
        return (subject, body);
    }

    private (string subject, string body) BuildNotOrderedBody(Event ev, RecipientLine recipient, string deadlineLocal)
    {
        var inviteeRecord = db.Invitees
            .FirstOrDefault(i => i.EventId == ev.Id && i.Email == recipient.Email);
        var inviteUrl = inviteeRecord is not null
            ? $"{_emailSettings.BaseUrl}/e/{ev.Slug}?invite={inviteeRecord.Id}"
            : $"{_emailSettings.BaseUrl}/e/{ev.Slug}";

        var subject = $"Reminder: submit your order for \"{ev.Title}\"";
        var body = $"""
            Hi,

            A reminder that you haven't submitted your order yet for "{ev.Title}".

            Submit your order here:
            {inviteUrl}

            Order deadline: {deadlineLocal} ({ev.TimeZoneId})
            """;
        return (subject, body);
    }
}
