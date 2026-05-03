using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Models;
using Moeltid.Services.Email;

namespace Moeltid.Services.Attendances;

public class AttendanceService(
    AppDbContext db,
    TokenGenerator tokenGenerator,
    IEmailSender emailSender,
    ILogger<AttendanceService> logger) : IAttendanceService
{
    private const int MaxTokenAttempts = 3;

    public async Task<Attendance> CreateAsync(CreateAttendanceRequest request)
    {
        ValidateOrderPayload(request.OrderType, request.MealOptionId, request.FreeTextOrder);

        for (var attempt = 0; attempt < MaxTokenAttempts; attempt++)
        {
            var attendance = new Attendance
            {
                Id = Guid.NewGuid(),
                EventId = request.EventId,
                Name = request.Name.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
                EditToken = tokenGenerator.RandomUrlSafeString(22),
                OrderType = request.OrderType,
                MealOptionId = request.OrderType == OrderType.PresetOption ? request.MealOptionId : null,
                FreeTextOrder = request.OrderType == OrderType.FreeText ? request.FreeTextOrder?.Trim() : null,
                SubmittedAt = DateTimeOffset.UtcNow,
            };

            try
            {
                db.Attendances.Add(attendance);
                await db.SaveChangesAsync();

                if (attendance.Email is not null)
                    await SendEditLinkEmailAsync(attendance);

                return attendance;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxTokenAttempts - 1)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("EditToken collision on attempt {Attempt}, retrying.", attempt + 1);
            }
        }

        throw new InvalidOperationException("Could not generate a unique edit token after multiple attempts.");
    }

    public Task<Attendance?> GetByEditTokenAsync(string editToken) =>
        db.Attendances.FirstOrDefaultAsync(a => a.EditToken == editToken);

    public async Task<Attendance> UpdateAsync(Guid id, UpdateAttendanceRequest request)
    {
        var attendance = await db.Attendances.FindAsync(id)
            ?? throw new InvalidOperationException($"Attendance {id} not found.");

        ValidateOrderPayload(request.OrderType, request.MealOptionId, request.FreeTextOrder);

        attendance.OrderType = request.OrderType;
        attendance.MealOptionId = request.OrderType == OrderType.PresetOption ? request.MealOptionId : null;
        attendance.FreeTextOrder = request.OrderType == OrderType.FreeText ? request.FreeTextOrder?.Trim() : null;

        await db.SaveChangesAsync();
        return attendance;
    }

    public async Task DeleteAsync(Guid id)
    {
        var attendance = await db.Attendances.FindAsync(id)
            ?? throw new InvalidOperationException($"Attendance {id} not found.");

        db.Attendances.Remove(attendance);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Attendance>> ListByEventAsync(Guid eventId)
    {
        var list = await db.Attendances
            .Where(a => a.EventId == eventId)
            .Include(a => a.MealOption)
            .ToListAsync();

        return [.. list.OrderBy(a => a.SubmittedAt)];
    }

    private async Task SendEditLinkEmailAsync(Attendance attendance)
    {
        var ev = await db.Events.FindAsync(attendance.EventId);
        var editPath = $"/e/{ev?.Slug}?t={attendance.EditToken}";
        var subject = $"Your order for \"{ev?.Title}\"";
        var body = $"""
            Hi {attendance.Name},

            Your order has been submitted for "{ev?.Title}".

            To update or withdraw your order, use this link (save it — it's your only way back):
            {editPath}

            Do not share this link — anyone with it can edit your order.
            """;

        await emailSender.SendAsync(attendance.Email!, subject, body);
    }

    private static void ValidateOrderPayload(OrderType orderType, Guid? mealOptionId, string? freeTextOrder)
    {
        if (orderType == OrderType.FreeText && string.IsNullOrWhiteSpace(freeTextOrder))
            throw new ArgumentException("FreeTextOrder is required when OrderType is FreeText.");

        if (orderType == OrderType.PresetOption && mealOptionId is null)
            throw new ArgumentException("MealOptionId is required when OrderType is PresetOption.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE") == true;
}
