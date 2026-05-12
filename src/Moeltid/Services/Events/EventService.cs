using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moeltid.Data;
using Moeltid.Models;
using Moeltid.Services.Email;
using Moeltid.Services.Reminders;

namespace Moeltid.Services.Events;

public class EventService(
    AppDbContext db,
    SlugGenerator slugGenerator,
    TokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
    IReminderService reminderService,
    ILogger<EventService> logger) : IEventService
{
    private const int MaxSlugAttempts = 3;
    private readonly EmailSettings _emailSettings = emailSettings.Value;

    public async Task<Event> CreateAsync(CreateEventRequest request)
    {
        // Deduplicate invitee emails within the batch before any retry loop
        var inviteeEmails = (request.InviteeEmails ?? [])
            .Select(e => e.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .ToList();

        for (var attempt = 0; attempt < MaxSlugAttempts; attempt++)
        {
            var ev = new Event
            {
                Id = Guid.NewGuid(),
                Slug = slugGenerator.Generate(request.Title),
                Title = request.Title,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                StartsAt = TimeZoneHelper.ToUtc(request.StartsAt, request.TimeZoneId),
                Deadline = TimeZoneHelper.ToUtc(request.Deadline, request.TimeZoneId),
                TimeZoneId = request.TimeZoneId,
                AllowFreeText = request.AllowFreeText,
                AttendeeOrdersVisible = request.AttendeeOrdersVisible,
                IsPrivate = request.IsPrivate,
                OwnerName = request.OwnerName.Trim(),
                OwnerEmail = request.OwnerEmail.Trim().ToLowerInvariant(),
                ManageToken = tokenGenerator.RandomUrlSafeString(22),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            // Build child entities keyed to this attempt's event ID
            var options = (request.MealOptions ?? [])
                .Select(d => new MealOption
                {
                    Id = Guid.NewGuid(),
                    EventId = ev.Id,
                    Label = d.Label.Trim(),
                    Tags = d.Tags,
                })
                .ToList();

            var now = DateTimeOffset.UtcNow;
            var invitees = inviteeEmails
                .Select(email => new Invitee
                {
                    Id = Guid.NewGuid(),
                    EventId = ev.Id,
                    Email = email,
                    InvitedAt = now,
                })
                .ToList();

            try
            {
                db.Events.Add(ev);
                if (options.Count > 0) db.MealOptions.AddRange(options);
                if (invitees.Count > 0) db.Invitees.AddRange(invitees);
                await db.SaveChangesAsync();   // single transaction

                await SendManageLinkEmailAsync(ev);
                foreach (var invitee in invitees)
                    await SendInviteEmailAsync(ev, invitee);

                return ev;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxSlugAttempts - 1)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("Slug or token collision on attempt {Attempt}, retrying.", attempt + 1);
            }
        }

        throw new InvalidOperationException("Could not generate a unique event slug after multiple attempts.");
    }

    public Task<Event?> GetByIdAsync(Guid id) =>
        db.Events.FindAsync(id).AsTask();

    public Task<Event?> GetBySlugAsync(string slug) =>
        db.Events.FirstOrDefaultAsync(e => e.Slug == slug);

    public async Task<Event> UpdateAsync(Guid id, UpdateEventRequest request)
    {
        var ev = await db.Events.FindAsync(id)
            ?? throw new InvalidOperationException($"Event {id} not found.");

        ev.Title = request.Title.Trim();
        ev.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ev.StartsAt = TimeZoneHelper.ToUtc(request.StartsAt, request.TimeZoneId);
        ev.Deadline = TimeZoneHelper.ToUtc(request.Deadline, request.TimeZoneId);
        ev.TimeZoneId = request.TimeZoneId;
        ev.AllowFreeText = request.AllowFreeText;
        ev.AttendeeOrdersVisible = request.AttendeeOrdersVisible;
        ev.IsPrivate = request.IsPrivate;

        await db.SaveChangesAsync();
        return ev;
    }

    public async Task<IReadOnlyList<EventListRow>> ListPublicAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // Fetch all public events with attendance counts (correlated subquery)
        var rows = await db.Events
            .Where(e => !e.IsPrivate)
            .Select(e => new
            {
                Event = e,
                OrderedCount = db.Attendances.Count(a => a.EventId == e.Id),
            })
            .ToListAsync();

        // IsOngoing evaluated client-side (DateTimeOffset comparison — SQLite limitation)
        return [.. rows.Select(r => new EventListRow(
            r.Event,
            r.OrderedCount,
            IsOngoing: !r.Event.IsClosed && r.Event.StartsAt > now))];
    }

    public async Task<Event> CloseAsync(Guid id)
    {
        var ev = await db.Events.FindAsync(id)
            ?? throw new InvalidOperationException($"Event {id} not found.");

        ev.IsClosed = true;
        await db.SaveChangesAsync();

        // Cancel any pending reminder — a closed event shouldn't send "you haven't ordered yet"
        await reminderService.CancelAsync(id);

        return ev;
    }

    public async Task<string> RotateManageTokenAsync(Guid id)
    {
        var ev = await db.Events.FindAsync(id)
            ?? throw new InvalidOperationException($"Event {id} not found.");

        ev.ManageToken = tokenGenerator.RandomUrlSafeString(22);
        await db.SaveChangesAsync();
        return ev.ManageToken;
    }

    public async Task<IReadOnlyList<Event>> GetByOwnerEmailAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var list = await db.Events
            .Where(e => e.OwnerEmail == normalised)
            .ToListAsync();
        return [.. list.OrderByDescending(e => e.CreatedAt)];
    }

    private async Task SendInviteEmailAsync(Event ev, Invitee invitee)
    {
        var inviteUrl = $"{_emailSettings.BaseUrl}/e/{ev.Slug}?invite={invitee.Id}";
        var startsAtLocal = TimeZoneHelper.ToLocalString(ev.StartsAt, ev.TimeZoneId, "yyyy-MM-dd HH:mm");
        var deadlineLocal = TimeZoneHelper.ToLocalString(ev.Deadline, ev.TimeZoneId, "yyyy-MM-dd HH:mm");
        var subject = $"You're invited to \"{ev.Title}\"";
        var body = $"""
            Hi,

            You've been invited to submit your order for "{ev.Title}".

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
                "Failed to send invite email to {Email} for event {EventId}.",
                invitee.Email, ev.Id);
        }
    }

    private async Task SendManageLinkEmailAsync(Event ev)
    {
        var manageUrl = $"{_emailSettings.BaseUrl}/e/{ev.Slug}/manage?t={ev.ManageToken}";
        var publicUrl = $"{_emailSettings.BaseUrl}/e/{ev.Slug}";
        var subject = $"Your manage link for \"{ev.Title}\"";
        var body = $"""
            Hi {ev.OwnerName},

            Your event "{ev.Title}" has been created.

            Manage your event here (save this link — it's your only way back in):
            {manageUrl}

            Share the public URL with attendees:
            {publicUrl}
            """;

        try
        {
            await emailSender.SendAsync(ev.OwnerEmail, subject, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send manage-link email to {Email} for event {EventId}.",
                ev.OwnerEmail, ev.Id);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE") == true;
}
