using Moeltid.Models;

namespace Moeltid.Services.MyEvents;

/// <summary>
/// Pure helper — no DB, no I/O.
/// Aggregates owned events + attendances + invitees for a given email into a deduplicated,
/// ordered list of <see cref="MyEventRow"/> objects. Same pattern as ReminderAudience / CsvExportBuilder.
/// </summary>
public static class MyEventsListBuilder
{
    /// <param name="ownedEvents">Events where <c>OwnerEmail</c> matches the requesting email.</param>
    /// <param name="attendances">Attendances (with <c>Event</c> included) where <c>Email</c> matches.</param>
    /// <param name="invitees">Invitees (with <c>Event</c> included) where <c>Email</c> matches.</param>
    /// <param name="baseUrl">Base URL for generating absolute action URLs.</param>
    /// <param name="now">Current time; controls the IsOngoing flag. Defaults to UtcNow when null.</param>
    public static IReadOnlyList<MyEventRow> Build(
        IReadOnlyList<Event> ownedEvents,
        IReadOnlyList<Attendance> attendances,
        IReadOnlyList<Invitee> invitees,
        string baseUrl,
        DateTimeOffset? now = null)
    {
        var utcNow = now ?? DateTimeOffset.UtcNow;

        // Collect all events by Id, accumulating roles as we go
        var roleMap = new Dictionary<Guid, (Event Ev, EventRole Roles, string? ManageToken, string? EditToken, Guid? InviteeId)>();

        foreach (var ev in ownedEvents)
        {
            if (!roleMap.TryGetValue(ev.Id, out var entry))
                entry = (ev, EventRole.None, ev.ManageToken, null, null);
            roleMap[ev.Id] = entry with { Roles = entry.Roles | EventRole.Owner, ManageToken = ev.ManageToken };
        }

        foreach (var a in attendances)
        {
            if (a.Event is null) continue;
            if (!roleMap.TryGetValue(a.EventId, out var entry))
                entry = (a.Event, EventRole.None, null, a.EditToken, null);
            roleMap[a.EventId] = entry with { Roles = entry.Roles | EventRole.Attendee, EditToken = a.EditToken };
        }

        foreach (var inv in invitees)
        {
            if (inv.Event is null) continue;
            if (!roleMap.TryGetValue(inv.EventId, out var entry))
                entry = (inv.Event, EventRole.None, null, null, inv.Id);
            roleMap[inv.EventId] = entry with { Roles = entry.Roles | EventRole.Invitee, InviteeId = inv.Id };
        }

        var rows = roleMap.Values
            .Select(e =>
            {
                var isOngoing = !e.Ev.IsClosed && e.Ev.StartsAt > utcNow;
                var actionUrl = ResolveActionUrl(baseUrl, e.Ev, e.Roles, e.ManageToken, e.EditToken, e.InviteeId);
                return new MyEventRow(e.Ev, e.Roles, actionUrl, isOngoing);
            })
            .ToList();

        // Ongoing first (ascending StartsAt), then past (descending StartsAt)
        return [..
            rows.Where(r => r.IsOngoing).OrderBy(r => r.Event.StartsAt)
            .Concat(rows.Where(r => !r.IsOngoing).OrderByDescending(r => r.Event.StartsAt))
        ];
    }

    private static string ResolveActionUrl(
        string baseUrl,
        Event ev,
        EventRole roles,
        string? manageToken,
        string? editToken,
        Guid? inviteeId)
    {
        // Highest-privilege role wins
        if (roles.HasFlag(EventRole.Owner) && manageToken is not null)
            return $"{baseUrl}/e/{ev.Slug}/manage?t={manageToken}";

        if (roles.HasFlag(EventRole.Attendee) && editToken is not null)
            return $"{baseUrl}/e/{ev.Slug}?t={editToken}";

        if (roles.HasFlag(EventRole.Invitee) && inviteeId is not null)
            return $"{baseUrl}/e/{ev.Slug}?invite={inviteeId}";

        // Fallback — should not normally be reached
        return $"{baseUrl}/e/{ev.Slug}";
    }
}
