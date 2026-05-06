using Moeltid.Models;

namespace Moeltid.Services.Attendances;

public interface IAttendanceService
{
    Task<Attendance> CreateAsync(CreateAttendanceRequest request);
    Task<Attendance?> GetByEditTokenAsync(string editToken);
    Task<Attendance> UpdateAsync(Guid id, UpdateAttendanceRequest request);
    Task DeleteAsync(Guid id);
    Task DeleteByOwnerAsync(Guid attendanceId);
    Task<IReadOnlyList<Attendance>> ListByEventAsync(Guid eventId);
}

public record CreateAttendanceRequest(
    Guid EventId,
    string Name,
    string? Email,
    OrderType OrderType,
    Guid? MealOptionId,
    string? FreeTextOrder
);

public record UpdateAttendanceRequest(
    OrderType OrderType,
    Guid? MealOptionId,
    string? FreeTextOrder
);
