using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Services.Exports;

namespace Moeltid.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        // GET /e/{slug}/manage/orders.csv?t={manageToken}
        // Read-only; exempt from the Phase 4 "no minimal-API for manage actions" rule because
        // downloads are not mutating and have no antiforgery/cookie/HttpContext-render concerns.
        // Returns 404 for invalid token or unknown slug — same generic response as the
        // invalid-manage-link Blazor view (don't leak whether a slug exists).
        app.MapGet("/e/{slug}/manage/orders.csv", async (
            string slug,
            string? t,
            AppDbContext db) =>
        {
            var ev = await db.Events.FirstOrDefaultAsync(e => e.Slug == slug);
            if (ev is null || ev.ManageToken != t)
                return Results.NotFound();

            var attendances = await db.Attendances
                .Where(a => a.EventId == ev.Id)
                .Include(a => a.MealOption)
                .OrderBy(a => a.SubmittedAt)
                .ToListAsync();

            var invitees = await db.Invitees
                .Where(i => i.EventId == ev.Id)
                .OrderBy(i => i.InvitedAt)
                .ToListAsync();

            var bytes = CsvExportBuilder.Build(ev, attendances, invitees);

            // Date in event's owner TZ for the file name
            var tz = SafeGetTz(ev.TimeZoneId);
            var dateStr = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).ToString("yyyy-MM-dd");
            var fileName = $"event-{ev.Slug}-orders-{dateStr}.csv";

            return Results.File(bytes, "text/csv; charset=utf-8", fileName);
        });
    }

    private static TimeZoneInfo SafeGetTz(string ianaId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
        catch { return TimeZoneInfo.Utc; }
    }
}
