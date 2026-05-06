using Moeltid.Models;

namespace Moeltid.Services.Invitees;

public interface IInviteeService
{
    /// <summary>Creates a single invitee for an event. Throws <see cref="InvalidOperationException"/>
    /// if the email already exists as an invitee or has an attendance for this event.</summary>
    Task<Invitee> CreateAsync(Guid eventId, string email);

    /// <summary>Creates a batch of invitees. Emails are lower-cased and deduplicated within
    /// the batch before insert. Already-existing invitees and attendees are skipped silently
    /// (idempotent — safe to call at event-creation time where no rows exist yet).</summary>
    Task<IReadOnlyList<Invitee>> CreateBatchAsync(Guid eventId, IEnumerable<string> emails);

    Task<Invitee?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Invitee>> ListByEventAsync(Guid eventId);

    /// <summary>Returns invitees who have no matching <see cref="Attendance"/> email for this event.</summary>
    Task<IReadOnlyList<Invitee>> ListUnorderedByEventAsync(Guid eventId);

    /// <summary>Deletes the invitee. When <paramref name="alsoDeleteMatchingAttendance"/> is true,
    /// also deletes the attendance with the same email for the same event (if any), transactionally.</summary>
    Task DeleteAsync(Guid id, bool alsoDeleteMatchingAttendance);

    /// <summary>Sends a reminder email to every invitee who has not yet submitted an order.</summary>
    Task SendRemindersAsync(Guid eventId);
}
