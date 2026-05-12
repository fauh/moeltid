namespace Moeltid.Services.Events;

/// <summary>
/// Pure helper — no DB, no I/O.
/// Splits and orders a flat list of <see cref="EventListRow"/>s into
/// (ongoing, past) for the public /events browse page.
/// Same pattern as AttendanceVisibility / EventDisplayList / ReminderAudience.
/// </summary>
public static class PublicEventGrouping
{
    public record Result(
        IReadOnlyList<EventListRow> Ongoing,
        IReadOnlyList<EventListRow> Past);

    /// <summary>
    /// Ongoing rows ordered by <c>StartsAt</c> ascending (soonest first).
    /// Past rows ordered by <c>StartsAt</c> descending (most-recent first).
    /// </summary>
    public static Result Build(IReadOnlyList<EventListRow> rows)
    {
        var ongoing = rows
            .Where(r => r.IsOngoing)
            .OrderBy(r => r.Event.StartsAt)
            .ToList();

        var past = rows
            .Where(r => !r.IsOngoing)
            .OrderByDescending(r => r.Event.StartsAt)
            .ToList();

        return new Result(ongoing, past);
    }
}
