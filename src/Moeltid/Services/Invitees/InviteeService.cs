using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Models;
using Moeltid.Services.Email;

namespace Moeltid.Services.Invitees;

public class InviteeService(
    AppDbContext db,
    IEmailSender emailSender,
    ILogger<InviteeService> logger) : IInviteeService
{
    public async Task<Invitee> CreateAsync(Guid eventId, string email)
    {
        var normalised = email.Trim().ToLowerInvariant();

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
        // Deduplicate within the batch (case-insensitive, first occurrence wins)
        var deduped = emails
            .Select(e => e.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .ToList();

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

    public async Task SendRemindersAsync(Guid eventId)
    {
        var unordered = await ListUnorderedByEventAsync(eventId);
        var ev = await db.Events.FindAsync(eventId);
        if (ev is null) return;

        foreach (var invitee in unordered)
        {
            var inviteUrl = $"/e/{ev.Slug}?invite={invitee.Id}";
            var subject = $"Reminder: submit your order for \"{ev.Title}\"";
            var body = $"""
                Hi,

                A reminder that you haven't submitted your order yet for "{ev.Title}".

                Submit your order here:
                {inviteUrl}

                Event date: {ev.StartsAt:yyyy-MM-dd HH:mm} UTC
                Order deadline: {ev.Deadline:yyyy-MM-dd HH:mm} UTC
                """;
            await emailSender.SendAsync(invitee.Email, subject, body);
        }

        logger.LogInformation("Sent reminders to {Count} unordered invitees for event {EventId}.",
            unordered.Count, eventId);
    }
}
