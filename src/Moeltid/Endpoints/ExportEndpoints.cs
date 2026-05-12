using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Services;
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
            AppDbContext db,
            HttpContext httpContext) =>
        {
            var ev = await db.Events.FirstOrDefaultAsync(e => e.Slug == slug);
            if (ev is null || ev.ManageToken != t)
                return Results.NotFound();

            // OrderBy must happen client-side: SQLite cannot translate DateTimeOffset
            // columns in ORDER BY clauses (throws NotSupportedException at runtime).
            var attendances = (await db.Attendances
                .Where(a => a.EventId == ev.Id)
                .Include(a => a.MealOption)
                .ToListAsync())
                .OrderBy(a => a.SubmittedAt)
                .ToList();

            var invitees = (await db.Invitees
                .Where(i => i.EventId == ev.Id)
                .ToListAsync())
                .OrderBy(i => i.InvitedAt)
                .ToList();

            var bytes = CsvExportBuilder.Build(ev, attendances, invitees);

            // Date in event's owner TZ for the file name
            var tz = TimeZoneHelper.SafeGetTz(ev.TimeZoneId);
            var dateStr = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).ToString("yyyy-MM-dd");
            var fileName = $"event-{ev.Slug}-orders-{dateStr}.csv";

            // Prevent caching of sensitive order data
            httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            httpContext.Response.Headers.Pragma = "no-cache";
            httpContext.Response.Headers.Expires = "0";

            return Results.File(bytes, "text/csv; charset=utf-8", fileName);
        });
    }
}
