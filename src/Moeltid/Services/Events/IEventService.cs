using Moeltid.Models;

namespace Moeltid.Services.Events;

public interface IEventService
{
    Task<Event> CreateAsync(CreateEventRequest request);
    Task<Event?> GetByIdAsync(Guid id);
    Task<Event?> GetBySlugAsync(string slug);
}

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
