using Moeltid.Models;

namespace Moeltid.Services.Events;

public interface IEventService
{
    Task<Event> CreateAsync(CreateEventRequest request);
    Task<Event?> GetByIdAsync(Guid id);
    Task<Event?> GetBySlugAsync(string slug);
    Task<Event> UpdateAsync(Guid id, UpdateEventRequest request);
    Task<Event> CloseAsync(Guid id);
    Task<string> RotateManageTokenAsync(Guid id);
    Task<IReadOnlyList<Event>> GetByOwnerEmailAsync(string email);
}

public record UpdateEventRequest(
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime Deadline,
    string TimeZoneId,
    bool AllowFreeText,
    bool AttendeeOrdersVisible
);

public record CreateEventRequest(
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime Deadline,
    string TimeZoneId,
    string OwnerName,
    string OwnerEmail,
    bool AllowFreeText,
    bool AttendeeOrdersVisible
);
