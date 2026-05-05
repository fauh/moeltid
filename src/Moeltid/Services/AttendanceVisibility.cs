using Moeltid.Models;

namespace Moeltid.Services;

/// <summary>
/// Pure helper expressing the per-event "attendee orders visible" toggle rule.
/// Centralised so the rule can be unit-tested without driving a Razor page.
/// Originally task 3.16 in Phase 3, completed retroactively in the Phase 3 reopened review pass.
/// </summary>
public static class AttendanceVisibility
{
    /// <summary>
    /// Returns the attendances visible to the current viewer per the event's toggle.
    ///
    /// Rules (mirrors design.md §3 / §6):
    /// - <paramref name="attendeeOrdersVisible"/> = true  → return <paramref name="all"/> unchanged.
    /// - <paramref name="attendeeOrdersVisible"/> = false + <paramref name="myAttendanceId"/> non-null → return only the matching row.
    /// - <paramref name="attendeeOrdersVisible"/> = false + <paramref name="myAttendanceId"/> null → return empty.
    /// </summary>
    public static IEnumerable<Attendance> Apply(
        IEnumerable<Attendance> all,
        bool attendeeOrdersVisible,
        Guid? myAttendanceId)
    {
        if (attendeeOrdersVisible) return all;
        if (myAttendanceId is null) return [];
        return all.Where(a => a.Id == myAttendanceId);
    }
}
