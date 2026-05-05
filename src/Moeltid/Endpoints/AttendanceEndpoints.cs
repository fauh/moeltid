using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Moeltid.Models;
using Moeltid.Services.Attendances;
using Moeltid.Services.Events;

namespace Moeltid.Endpoints;

public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this WebApplication app)
    {
        // POST /e/{slug}/order — submit a new order
        app.MapPost("/e/{slug}/order", async (
            string slug,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IEventService eventService,
            IAttendanceService attendanceService) =>
        {
            try { await antiforgery.ValidateRequestAsync(httpContext); }
            catch { return Results.BadRequest("Invalid or missing antiforgery token."); }

            var ev = await eventService.GetBySlugAsync(slug);
            if (ev is null) return Results.NotFound();
            if (ev.IsClosed) return Results.BadRequest("This event is closed.");

            var form = await httpContext.Request.ReadFormAsync();
            var name = form["name"].ToString().Trim();
            var email = form["email"].ToString().Trim();
            var freeText = form["freeTextOrder"].ToString().Trim();
            var mealOptionIdStr = form["mealOptionId"].ToString();

            if (string.IsNullOrEmpty(name))
                return Results.BadRequest("Name is required.");

            // OrderType is derived from whether mealOptionId resolves to a Guid:
            // a non-empty value means a preset was picked; empty (or missing) means free-text.
            Guid? mealOptionId = Guid.TryParse(mealOptionIdStr, out var parsedId) ? parsedId : null;
            var orderType = mealOptionId.HasValue ? OrderType.PresetOption : OrderType.FreeText;

            try
            {
                var attendance = await attendanceService.CreateAsync(new CreateAttendanceRequest(
                    EventId: ev.Id,
                    Name: name,
                    Email: string.IsNullOrEmpty(email) ? null : email,
                    OrderType: orderType,
                    MealOptionId: mealOptionId,
                    FreeTextOrder: string.IsNullOrEmpty(freeText) ? null : freeText
                ));

                return Results.Redirect($"/e/{slug}?t={attendance.EditToken}");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        // POST /e/{slug}/order/{attendanceId} — update an existing order
        app.MapPost("/e/{slug}/order/{attendanceId:guid}", async (
            string slug,
            Guid attendanceId,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IEventService eventService,
            IAttendanceService attendanceService) =>
        {
            try { await antiforgery.ValidateRequestAsync(httpContext); }
            catch { return Results.BadRequest("Invalid or missing antiforgery token."); }

            var ev = await eventService.GetBySlugAsync(slug);
            if (ev is null) return Results.NotFound();
            if (ev.IsClosed) return Results.BadRequest("This event is closed.");

            var attendance = await attendanceService.GetByEditTokenAsync(
                (await httpContext.Request.ReadFormAsync())["editToken"].ToString());
            if (attendance is null || attendance.Id != attendanceId || attendance.EventId != ev.Id)
                return Results.Forbid();

            var form = httpContext.Request.Form;
            var freeText = form["freeTextOrder"].ToString().Trim();
            var mealOptionIdStr = form["mealOptionId"].ToString();

            // OrderType is derived from whether mealOptionId resolves to a Guid:
            // a non-empty value means a preset was picked; empty (or missing) means free-text.
            Guid? mealOptionId = Guid.TryParse(mealOptionIdStr, out var parsedId) ? parsedId : null;
            var orderType = mealOptionId.HasValue ? OrderType.PresetOption : OrderType.FreeText;

            try
            {
                await attendanceService.UpdateAsync(attendanceId, new UpdateAttendanceRequest(
                    OrderType: orderType,
                    MealOptionId: mealOptionId,
                    FreeTextOrder: string.IsNullOrEmpty(freeText) ? null : freeText
                ));

                return Results.Redirect($"/e/{slug}?t={attendance.EditToken}");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        // POST /e/{slug}/order/{attendanceId}/delete — withdraw an order
        app.MapPost("/e/{slug}/order/{attendanceId:guid}/delete", async (
            string slug,
            Guid attendanceId,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IEventService eventService,
            IAttendanceService attendanceService) =>
        {
            try { await antiforgery.ValidateRequestAsync(httpContext); }
            catch { return Results.BadRequest("Invalid or missing antiforgery token."); }

            var ev = await eventService.GetBySlugAsync(slug);
            if (ev is null) return Results.NotFound();
            if (ev.IsClosed) return Results.BadRequest("This event is closed.");

            var form = await httpContext.Request.ReadFormAsync();
            var attendance = await attendanceService.GetByEditTokenAsync(form["editToken"].ToString());
            if (attendance is null || attendance.Id != attendanceId || attendance.EventId != ev.Id)
                return Results.Forbid();

            await attendanceService.DeleteAsync(attendanceId);
            return Results.Redirect($"/e/{slug}");
        });
    }
}
