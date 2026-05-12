using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moeltid.Data;
using Moeltid.Models;
using Moeltid.Services.Email;

namespace Moeltid.Services.Invitees;

public class InviteeService(
    AppDbContext db,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
    ILogger<InviteeService> logger) : IInviteeService
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private readonly EmailSettings _emailSettings = emailSettings.Value;

    public async Task<Invitee> CreateAsync(Guid eventId, string email)
    {
        var normalised = email.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalised) || !EmailValidator.IsValid(normalised))
            throw new InvalidOperationException($"\"{email}\" is not a valid email address.");

        // Refuse if already invited
        var existingInvitee = await db.Invitees
            .AnyAsync(i => i.EventId == eventId && i.Email == normalised);
        if (existingInvitee)
            throw new InvalidOperationException($"{normalised} is already invited to this event.");

        // Refuse if already has an attendance (case-insensitive via value converters)
        var existingAttendance = await db.Attendances
            .AnyAsync(a => a.EventId == eventId && a.Email == normalised);
        if (existingAttendance)
            throw new InvalidOperationException($"{normalised} has already submitted an order for this event.");

        var invitee = new Invitee
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Email = normalised,
            InvitedAt = DateTimeOffset.UtcNow,
        };
        db.Invitees.Add(invitee);
        await db.SaveChangesAsync();
        return invitee;
    }

    public async Task<IReadOnlyList<Invitee>> CreateBatchAsync(Guid eventId, IEnumerable<string> emails)
    {
        // Deduplicate within the batch (case-insensitive, first occurrence wins).
        // Silently skip malformed emails — matches the skip-on-duplicate pattern below.
        // Skipped count is logged at the end so it's visible in dev/log streams.
        var invalidCount = 0;
        var deduped = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in emails ?? [])
        {
            var normalised = raw.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalised)) continue;
            if (!EmailValidator.IsValid(normalised))
            {
                invalidCount++;
                continue;
            }
            if (seen.Add(normalised))
                deduped.Add(normalised);
        }

        if (invalidCount > 0)
            logger.LogInformation("Skipped {Count} malformed email(s) when batch-adding invitees to event {EventId}.",
                invalidCount, eventId);

        if (deduped.Count == 0)
            return [];

        // Skip emails that are already invitees or already have an attendance
        var existingInviteeEmails = await db.Invitees
            .Where(i => i.EventId == eventId && deduped.Contains(i.Email))
            .Select(i => i.Email)
            .ToListAsync();

        var existingAttendanceEmails = await db.Attendances
            .Where(a => a.EventId == eventId && a.Email != null && deduped.Contains(a.Email))
            .Select(a => a.Email!)
            .ToListAsync();

        var skip = existingInviteeEmails
            .Concat(existingAttendanceEmails)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toCreate = deduped
            .Where(e => !skip.Contains(e))
            .Select(e => new Invitee
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Email = e,
                InvitedAt = DateTimeOffset.UtcNow,
            })
            .ToList();

        if (toCreate.Count == 0)
            return [];

        db.Invitees.AddRange(toCreate);
        await db.SaveChangesAsync();

        if (skip.Count > 0)
            logger.LogInformation("Skipped {Count} already-existing emails when batch-adding invitees to event {EventId}.",
                skip.Count, eventId);

        return toCreate;
    }

    public Task<Invitee?> GetByIdAsync(Guid id) =>
        db.Invitees.FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IReadOnlyList<Invitee>> ListByEventAsync(Guid eventId)
    {
        var list = await db.Invitees
            .Where(i => i.EventId == eventId)
            .ToListAsync();
        return [.. list.OrderBy(i => i.InvitedAt)];
    }

    public async Task<IReadOnlyList<Invitee>> ListUnorderedByEventAsync(Guid eventId)
    {
        var list = await db.Invitees
            .Where(i => i.EventId == eventId)
            .Where(i => !db.Attendances.Any(a => a.EventId == eventId && a.Email == i.Email))
            .ToListAsync();
        return [.. list.OrderBy(i => i.InvitedAt)];
    }

    public async Task DeleteAsync(Guid id, bool alsoDeleteMatchingAttendance)
    {
        var invitee = await db.Invitees.FindAsync(id)
            ?? throw new InvalidOperationException($"Invitee {id} not found.");

        if (alsoDeleteMatchingAttendance)
        {
            var attendance = await db.Attendances
                .FirstOrDefaultAsync(a => a.EventId == invitee.EventId && a.Email == invitee.Email);
            if (attendance is not null)
                db.Attendances.Remove(attendance);
        }

        db.Invitees.Remove(invitee);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Invitee>> ListByEmailAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var list = await db.Invitees
            .Where(i => i.Email == normalised)
            .Include(i => i.Event)
            .ToListAsync();
        return [.. list.OrderBy(i => i.InvitedAt)];
    }

    public async Task SendRemindersAsync(Guid eventId)
    {
        var unordered = await ListUnorderedByEventAsync(eventId);
        var ev = await db.Events.FindAsync(eventId);
        if (ev is null) return;

        var startsAtLocal = TimeZoneHelper.ToLocalString(ev.StartsAt, ev.TimeZoneId, "yyyy-MM-dd HH:mm");
        var deadlineLocal = TimeZoneHelper.ToLocalString(ev.Deadline, ev.TimeZoneId, "yyyy-MM-dd HH:mm");

        foreach (var invitee in unordered)
        {
            var inviteUrl = $"{_emailSettings.BaseUrl}/e/{ev.Slug}?invite={invitee.Id}";
            var subject = $"Reminder: submit your order for \"{ev.Title}\"";
            var body = $"""
                Hi,

                A reminder that you haven't submitted your order yet for "{ev.Title}".

                Submit your order here:
                {inviteUrl}

                Event date: {startsAtLocal} ({ev.TimeZoneId})
                Order deadline: {deadlineLocal} ({ev.TimeZoneId})
                """;

            try
            {
                await emailSender.SendAsync(invitee.Email, subject, body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to send reminder email to {Email} for event {EventId}.",
                    invitee.Email, eventId);
            }
        }

        logger.LogInformation("Sent reminders to {Count} unordered invitees for event {EventId}.",
            unordered.Count, eventId);
    }
}
