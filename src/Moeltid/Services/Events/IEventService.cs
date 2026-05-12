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

    /// <summary>Returns all public events (IsPrivate == false) with ordered-count and IsOngoing flag.</summary>
    Task<IReadOnlyList<EventListRow>> ListPublicAsync();
}

/// <summary>Projection row used by the public /events browse page.</summary>
public record EventListRow(Event Event, int OrderedCount, bool IsOngoing);

public record UpdateEventRequest(
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime Deadline,
    string TimeZoneId,
    bool AllowFreeText,
    bool AttendeeOrdersVisible,
    bool IsPrivate = false
);

/// <summary>Defines a meal option to create alongside the event.</summary>
public record MealOptionDraft(string Label, MealTag Tags);

public record CreateEventRequest(
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime Deadline,
    string TimeZoneId,
    string OwnerName,
    string OwnerEmail,
    bool AllowFreeText,
    bool AttendeeOrdersVisible,
    IReadOnlyList<MealOptionDraft>? MealOptions = null,
    IReadOnlyList<string>? InviteeEmails = null,
    bool IsPrivate = false
);
