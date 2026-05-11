using Moeltid.Models;

namespace Moeltid.Services.Reminders;

public enum RecipientKind
{
    /// <summary>Has submitted an order — body says "you ordered X".</summary>
    HasOrdered,
    /// <summary>Invited but hasn't ordered yet — body says "submit before deadline".</summary>
    NotOrdered,
}

public sealed record RecipientLine(
    string Email,
    RecipientKind Kind,
    /// <summary>Human-readable order text (preset label or free-text), null for NotOrdered recipients.</summary>
    string? OrderText);

/// <summary>
/// Pure helper that builds the per-recipient audience for a scheduled reminder.
/// Attendees with email get a "you ordered X" line; invitees who haven't ordered get a "submit by deadline" line.
/// Attendees without email are excluded (can't email them).
/// </summary>
public static class ReminderAudience
{
    public static IReadOnlyList<RecipientLine> Build(
        IEnumerable<Attendance> attendances,
        IEnumerable<Invitee> invitees)
    {
        var result = new List<RecipientLine>();

        // Track which emails have an attendance so we don't double-send to invitees who ordered
        var orderedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attendance in attendances)
        {
            if (string.IsNullOrWhiteSpace(attendance.Email)) continue;

            orderedEmails.Add(attendance.Email);

            var orderText = attendance.OrderType switch
            {
                OrderType.PresetOption => attendance.MealOption?.Label ?? "a preset option",
                OrderType.FreeText => attendance.FreeTextOrder ?? "a custom order",
                _ => "an order",
            };

            result.Add(new RecipientLine(attendance.Email, RecipientKind.HasOrdered, orderText));
        }

        // Add invitees who haven't ordered
        foreach (var invitee in invitees)
        {
            if (orderedEmails.Contains(invitee.Email)) continue;
            result.Add(new RecipientLine(invitee.Email, RecipientKind.NotOrdered, null));
        }

        return result;
    }
}
