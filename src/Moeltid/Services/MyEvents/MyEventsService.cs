using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moeltid.Data;
using Moeltid.Models;
using Moeltid.Services.Attendances;
using Moeltid.Services.Email;
using Moeltid.Services.Events;
using Moeltid.Services.Invitees;

namespace Moeltid.Services.MyEvents;

public class MyEventsService(
    AppDbContext db,
    TokenGenerator tokenGenerator,
    IEventService eventService,
    IAttendanceService attendanceService,
    IInviteeService inviteeService,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
    ILogger<MyEventsService> logger) : IMyEventsService
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private readonly EmailSettings _emailSettings = emailSettings.Value;

    public async Task RequestAccessAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalised) || !EmailValidator.IsValid(normalised))
            throw new ArgumentException($"\"{email}\" is not a valid email address.", nameof(email));

        var now = DateTimeOffset.UtcNow;
        var token = new MyEventsAccessToken
        {
            Token = tokenGenerator.RandomUrlSafeString(32),
            Email = normalised,
            IssuedAt = now,
            ExpiresAt = now.Add(TokenLifetime),
        };

        db.MyEventsAccessTokens.Add(token);
        await db.SaveChangesAsync();

        // Await the email send so any scoped dependencies it uses stay within the
        // current request scope and do not overlap with later work on this service.
        await SendMagicLinkEmailAsync(normalised, token.Token);
    }

    public async Task<string?> ValidateAndConsumeAsync(string token)
    {
        var row = await db.MyEventsAccessTokens.FindAsync(token);
        if (row is null) return null;
        if (row.ConsumedAt is not null) return null;
        if (row.ExpiresAt <= DateTimeOffset.UtcNow) return null;

        row.ConsumedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return row.Email;
    }

    public async Task<IReadOnlyList<MyEventRow>> GetEventsForEmailAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();

        var ownedEvents = await eventService.GetByOwnerEmailAsync(normalised);
        var attendances = await attendanceService.ListByEmailAsync(normalised);
        var invitees = await inviteeService.ListByEmailAsync(normalised);

        return MyEventsListBuilder.Build(ownedEvents, attendances, invitees, _emailSettings.BaseUrl);
    }

    // ── email ──────────────────────────────────────────────────────────────────

    private async Task SendMagicLinkEmailAsync(string email, string token)
    {
        try
        {
            var rows = await GetEventsForEmailAsync(email);
            var magicLink = $"{_emailSettings.BaseUrl}/my-events?t={token}";

            var subject = "Your events on Consid Måltid";
            var body = BuildEmailBody(rows, magicLink);

            await emailSender.SendAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send my-events magic-link email to {Email}.", email);
        }
    }

    private static string BuildEmailBody(IReadOnlyList<MyEventRow> rows, string magicLink)
    {
        var ongoing = rows.Where(r => r.IsOngoing).ToList();
        var past = rows.Where(r => !r.IsOngoing).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Here are the events tied to your email address.");
        sb.AppendLine();

        if (rows.Count == 0)
        {
            sb.AppendLine("No events found for your email.");
        }
        else
        {
            if (ongoing.Count > 0)
            {
                sb.AppendLine("Upcoming events:");
                foreach (var row in ongoing)
                    AppendRow(sb, row);
                sb.AppendLine();
            }

            if (past.Count > 0)
            {
                sb.AppendLine("Past events:");
                foreach (var row in past)
                    AppendRow(sb, row);
                sb.AppendLine();
            }
        }

        sb.AppendLine("To see the full list in your browser, use this link (valid for 1 hour, one-time use):");
        sb.AppendLine(magicLink);
        sb.AppendLine();
        sb.AppendLine("If you did not request this email, you can safely ignore it.");

        return sb.ToString();
    }

    private static void AppendRow(System.Text.StringBuilder sb, MyEventRow row)
    {
        var ev = row.Event;
        var startsAtLocal = TimeZoneHelper.ToLocalString(ev.StartsAt, ev.TimeZoneId, "yyyy-MM-dd HH:mm");
        var roles = RoleLabel(row.Roles);
        sb.AppendLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "  • {0} — {1} ({2}) [{3}]",
            ev.Title, startsAtLocal, ev.TimeZoneId, roles));
        sb.AppendLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "    {0}", row.ActionUrl));
    }

    private static string RoleLabel(EventRole roles)
    {
        var parts = new List<string>();
        if (roles.HasFlag(EventRole.Owner)) parts.Add("owner");
        if (roles.HasFlag(EventRole.Attendee)) parts.Add("attendee");
        if (roles.HasFlag(EventRole.Invitee)) parts.Add("invitee");
        return string.Join(", ", parts);
    }
}
