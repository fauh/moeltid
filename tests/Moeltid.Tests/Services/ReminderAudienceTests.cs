using Moeltid.Models;
using Moeltid.Services.Reminders;
using Shouldly;

namespace Moeltid.Tests.Services;

/// <summary>
/// Unit tests for ReminderAudience.Build — pure function, no DB needed.
/// </summary>
public class ReminderAudienceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static Attendance MakeAttendance(string email, string? freeText = null, MealOption? option = null)
    {
        var hasOption = option is not null;
        return new Attendance
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Name = "Test",
            Email = email.ToLowerInvariant(),
            EditToken = Guid.NewGuid().ToString("N"),
            OrderType = hasOption ? OrderType.PresetOption : OrderType.FreeText,
            MealOptionId = option?.Id,
            MealOption = option,
            FreeTextOrder = freeText,
            SubmittedAt = DateTimeOffset.UtcNow,
        };
    }

    private static Invitee MakeInvitee(string email) => new()
    {
        Id = Guid.NewGuid(),
        EventId = Guid.NewGuid(),
        Email = email.ToLowerInvariant(),
        InvitedAt = DateTimeOffset.UtcNow,
    };

    private static MealOption MakeOption(string label) => new()
    {
        Id = Guid.NewGuid(),
        EventId = Guid.NewGuid(),
        Label = label,
        Tags = MealTag.None,
    };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_AttendeesOnly_ReturnsHasOrderedLines()
    {
        var opt = MakeOption("Salmon");
        var attendances = new[]
        {
            MakeAttendance("a@example.com", option: opt),
            MakeAttendance("b@example.com", freeText: "Veggie wrap"),
        };

        var result = ReminderAudience.Build(attendances, []);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.Kind == RecipientKind.HasOrdered);
        result.ShouldContain(r => r.Email == "a@example.com" && r.OrderText == "Salmon");
        result.ShouldContain(r => r.Email == "b@example.com" && r.OrderText == "Veggie wrap");
    }

    [Fact]
    public void Build_InviteesOnly_ReturnsNotOrderedLines()
    {
        var invitees = new[] { MakeInvitee("c@example.com"), MakeInvitee("d@example.com") };

        var result = ReminderAudience.Build([], invitees);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.Kind == RecipientKind.NotOrdered);
        result.ShouldAllBe(r => r.OrderText == null);
    }

    [Fact]
    public void Build_Mixed_InviteeWhoOrderedIsNotDoubled()
    {
        // inv@example.com is both an invitee AND has an attendance — should appear
        // only as HasOrdered (not duplicated as NotOrdered).
        var opt = MakeOption("Pasta");
        var attendances = new[] { MakeAttendance("inv@example.com", option: opt) };
        var invitees = new[]
        {
            MakeInvitee("inv@example.com"),  // already ordered
            MakeInvitee("pending@example.com"),  // not ordered
        };

        var result = ReminderAudience.Build(attendances, invitees);

        result.Count.ShouldBe(2);
        result.ShouldContain(r => r.Email == "inv@example.com" && r.Kind == RecipientKind.HasOrdered);
        result.ShouldContain(r => r.Email == "pending@example.com" && r.Kind == RecipientKind.NotOrdered);
    }

    [Fact]
    public void Build_MixedCaseEmails_MatchesCaseInsensitively()
    {
        // Attendance stored with lower-case email; invitee stored with original case
        var attendance = MakeAttendance("mixed@example.com");
        var invitee = MakeInvitee("MIXED@EXAMPLE.COM");

        var result = ReminderAudience.Build([attendance], [invitee]);

        // The invitee should be suppressed because the attendance email matches
        result.Count.ShouldBe(1);
        result[0].Kind.ShouldBe(RecipientKind.HasOrdered);
    }

    [Fact]
    public void Build_AttendeeWithNoEmail_IsExcluded()
    {
        // Simulate anonymous attendee (no email)
        var noEmailAttendance = new Attendance
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Name = "Anonymous",
            Email = null,
            EditToken = Guid.NewGuid().ToString("N"),
            OrderType = OrderType.FreeText,
            FreeTextOrder = "Anything",
            SubmittedAt = DateTimeOffset.UtcNow,
        };

        var result = ReminderAudience.Build([noEmailAttendance], []);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_Empty_ReturnsEmpty()
    {
        var result = ReminderAudience.Build([], []);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_StatusAwareBodies_PresetOptionUsesLabel()
    {
        var opt = MakeOption("Beef Wellington");
        var attendance = MakeAttendance("fancy@example.com", option: opt);

        var result = ReminderAudience.Build([attendance], []);

        result[0].OrderText.ShouldBe("Beef Wellington");
    }

    [Fact]
    public void Build_StatusAwareBodies_FreeTextUsesOrderText()
    {
        var attendance = MakeAttendance("custom@example.com", freeText: "No nuts please");

        var result = ReminderAudience.Build([attendance], []);

        result[0].OrderText.ShouldBe("No nuts please");
    }
}
