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

            try
            {
                db.Events.Add(ev);
                await db.SaveChangesAsync();

                await SendManageLinkEmailAsync(ev);
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
