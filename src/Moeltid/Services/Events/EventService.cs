using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Models;
using Moeltid.Services.Email;

namespace Moeltid.Services.Events;

public class EventService(
    AppDbContext db,
    SlugGenerator slugGenerator,
    TokenGenerator tokenGenerator,
    IEmailSender emailSender,
    ILogger<EventService> logger) : IEventService
{
    private const int MaxSlugAttempts = 3;

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

        await db.SaveChangesAsync();
        return ev;
    }

    public async Task<Event> CloseAsync(Guid id)
    {
        var ev = await db.Events.FindAsync(id)
            ?? throw new InvalidOperationException($"Event {id} not found.");

        ev.IsClosed = true;
        await db.SaveChangesAsync();
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
        var inviteUrl = $"/e/{ev.Slug}?invite={invitee.Id}";
        var subject = $"You're invited to \"{ev.Title}\"";
        var body = $"""
            Hi,

            You've been invited to submit your order for "{ev.Title}".

            Submit your order here:
            {inviteUrl}

            Event date: {ev.StartsAt:yyyy-MM-dd HH:mm} UTC
            Order deadline: {ev.Deadline:yyyy-MM-dd HH:mm} UTC
            """;
        await emailSender.SendAsync(invitee.Email, subject, body);
    }

    private async Task SendManageLinkEmailAsync(Event ev)
    {
        var managePath = $"/e/{ev.Slug}/manage?t={ev.ManageToken}";
        var subject = $"Your manage link for \"{ev.Title}\"";
        var body = $"""
            Hi {ev.OwnerName},

            Your event "{ev.Title}" has been created.

            Manage your event here (save this link — it's your only way back in):
            {managePath}

            Share the public URL with attendees:
            /e/{ev.Slug}
            """;

        await emailSender.SendAsync(ev.OwnerEmail, subject, body);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE") == true;
}
