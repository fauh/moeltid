using Moeltid.Models;

namespace Moeltid.Services;

public enum DisplayRowKind { Ordered, NoOrderYet }

public sealed class DisplayRow
{
    public DisplayRowKind Kind { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="DisplayRowKind.Ordered"/>.</summary>
    public Attendance? Attendance { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="DisplayRowKind.NoOrderYet"/>.</summary>
    public Invitee? Invitee { get; init; }

    /// <summary>The email that identifies this row (whichever object is set).</summary>
    public string? Email => Attendance?.Email ?? Invitee?.Email;
}

/// <summary>
/// Merges attendances and invitees into a single display list.
/// Invitees whose email matches an existing attendance are excluded from the
/// "no order yet" set — they are represented by their attendance row instead.
/// Email comparison is case-insensitive (both are stored lower-cased).
/// </summary>
public static class EventDisplayList
{
    public static IReadOnlyList<DisplayRow> Build(
        IEnumerable<Attendance> attendances,
        IEnumerable<Invitee> invitees)
    {
        var attendanceList = attendances.ToList();

        var orderedEmails = attendanceList
            .Where(a => a.Email is not null)
            .Select(a => a.Email!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<DisplayRow>();

        foreach (var a in attendanceList)
            rows.Add(new DisplayRow { Kind = DisplayRowKind.Ordered, Attendance = a });

        foreach (var inv in invitees)
        {
            if (!orderedEmails.Contains(inv.Email))
                rows.Add(new DisplayRow { Kind = DisplayRowKind.NoOrderYet, Invitee = inv });
        }

        return rows;
    }
}
