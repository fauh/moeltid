using Moeltid.Models;
using Moeltid.Services.MyEvents;
using Shouldly;

namespace Moeltid.Tests.Services;

/// <summary>
/// Tests for MyEventsListBuilder — pure helper, no DB needed.
/// </summary>
public class MyEventsListBuilderTests
{
    private const string BaseUrl = "https://test.example";

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Event MakeEvent(
        string ownerEmail = "owner@example.com",
        bool isClosed = false,
        int daysUntilStart = 7) => new()
    {
        Id = Guid.NewGuid(),
        Slug = $"test-event-{Guid.NewGuid():N}",
        Title = "Test Event",
        StartsAt = DateTimeOffset.UtcNow.AddDays(daysUntilStart),
        Deadline = DateTimeOffset.UtcNow.AddDays(daysUntilStart - 1),
        TimeZoneId = "UTC",
        OwnerName = "Owner",
        OwnerEmail = ownerEmail,
        ManageToken = "manage-token-abc",
        CreatedAt = DateTimeOffset.UtcNow,
        IsClosed = isClosed,
    };

    private static Attendance MakeAttendance(Guid eventId, Event ev, string email = "attendee@example.com") => new()
    {
        Id = Guid.NewGuid(),
        EventId = eventId,
        Event = ev,
        Name = "Attendee",
        Email = email,
        EditToken = "edit-token-xyz",
        OrderType = OrderType.FreeText,
        FreeTextOrder = "Pasta",
        SubmittedAt = DateTimeOffset.UtcNow,
    };

    private static Invitee MakeInvitee(Guid eventId, Event ev, string email = "invitee@example.com") => new()
    {
        Id = Guid.NewGuid(),
        EventId = eventId,
        Event = ev,
        Email = email,
        InvitedAt = DateTimeOffset.UtcNow,
    };

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_OwnerOnly_ReturnsOwnerRoleAndManageUrl()
    {
        var ev = MakeEvent();
        var rows = MyEventsListBuilder.Build([ev], [], [], BaseUrl);

        rows.Count.ShouldBe(1);
        rows[0].Roles.HasFlag(EventRole.Owner).ShouldBeTrue();
        rows[0].ActionUrl.ShouldBe($"{BaseUrl}/e/{ev.Slug}/manage?t={ev.ManageToken}");
    }

    [Fact]
    public void Build_AttendeeOnly_ReturnsAttendeeRoleAndEditUrl()
    {
        var ev = MakeEvent();
        var attendance = MakeAttendance(ev.Id, ev);
        var rows = MyEventsListBuilder.Build([], [attendance], [], BaseUrl);

        rows.Count.ShouldBe(1);
        rows[0].Roles.HasFlag(EventRole.Attendee).ShouldBeTrue();
        rows[0].ActionUrl.ShouldBe($"{BaseUrl}/e/{ev.Slug}?t={attendance.EditToken}");
    }

    [Fact]
    public void Build_InviteeOnly_ReturnsInviteeRoleAndInviteUrl()
    {
        var ev = MakeEvent();
        var invitee = MakeInvitee(ev.Id, ev);
        var rows = MyEventsListBuilder.Build([], [], [invitee], BaseUrl);

        rows.Count.ShouldBe(1);
        rows[0].Roles.HasFlag(EventRole.Invitee).ShouldBeTrue();
        rows[0].ActionUrl.ShouldBe($"{BaseUrl}/e/{ev.Slug}?invite={invitee.Id}");
    }

    [Fact]
    public void Build_OwnerAndAttendee_SameEvent_CombinesRolesAndUsesManageUrl()
    {
        var ev = MakeEvent(ownerEmail: "dual@example.com");
        var attendance = MakeAttendance(ev.Id, ev, email: "dual@example.com");

        var rows = MyEventsListBuilder.Build([ev], [attendance], [], BaseUrl);

        rows.Count.ShouldBe(1); // not doubled
        rows[0].Roles.HasFlag(EventRole.Owner).ShouldBeTrue();
        rows[0].Roles.HasFlag(EventRole.Attendee).ShouldBeTrue();
        // Owner wins priority
        rows[0].ActionUrl.ShouldBe($"{BaseUrl}/e/{ev.Slug}/manage?t={ev.ManageToken}");
    }

    [Fact]
    public void Build_AttendeeAndInvitee_SameEvent_AttendeeUrlWins()
    {
        var ev = MakeEvent();
        var attendance = MakeAttendance(ev.Id, ev, email: "both@example.com");
        var invitee = MakeInvitee(ev.Id, ev, email: "both@example.com");

        var rows = MyEventsListBuilder.Build([], [attendance], [invitee], BaseUrl);

        rows.Count.ShouldBe(1);
        rows[0].Roles.HasFlag(EventRole.Attendee).ShouldBeTrue();
        rows[0].Roles.HasFlag(EventRole.Invitee).ShouldBeTrue();
        rows[0].ActionUrl.ShouldStartWith($"{BaseUrl}/e/{ev.Slug}?t=");
    }

    [Fact]
    public void Build_OngoingFlag_TrueForFutureOpenEvent()
    {
        var ongoing = MakeEvent(daysUntilStart: 7, isClosed: false);
        var rows = MyEventsListBuilder.Build([ongoing], [], [], BaseUrl);

        rows[0].IsOngoing.ShouldBeTrue();
    }

    [Fact]
    public void Build_OngoingFlag_FalseForClosedEvent()
    {
        var closed = MakeEvent(daysUntilStart: 7, isClosed: true);
        var rows = MyEventsListBuilder.Build([closed], [], [], BaseUrl);

        rows[0].IsOngoing.ShouldBeFalse();
    }

    [Fact]
    public void Build_OngoingFlag_FalseForPastEvent()
    {
        var past = MakeEvent(daysUntilStart: -3, isClosed: false); // started 3 days ago
        var rows = MyEventsListBuilder.Build([past], [], [], BaseUrl);

        rows[0].IsOngoing.ShouldBeFalse();
    }

    [Fact]
    public void Build_OrderingOngoingFirstThenPastDescending()
    {
        var now = DateTimeOffset.UtcNow;

        // ongoing events: soonest first
        var soon = MakeEvent(daysUntilStart: 2);
        var later = MakeEvent(daysUntilStart: 10);

        // past events: most-recent first
        var recentPast = MakeEvent(daysUntilStart: -1);
        var olderPast = MakeEvent(daysUntilStart: -10);

        var rows = MyEventsListBuilder.Build(
            [soon, later, recentPast, olderPast], [], [], BaseUrl, now);

        rows.Count.ShouldBe(4);

        // First two: ongoing (soonest first)
        rows[0].IsOngoing.ShouldBeTrue();
        rows[0].Event.StartsAt.ShouldBeLessThan(rows[1].Event.StartsAt);

        // Last two: past (most-recent first)
        rows[2].IsOngoing.ShouldBeFalse();
        rows[2].Event.StartsAt.ShouldBeGreaterThan(rows[3].Event.StartsAt);
    }

    [Fact]
    public void Build_EmptyInputs_ReturnsEmptyList()
    {
        var rows = MyEventsListBuilder.Build([], [], [], BaseUrl);
        rows.ShouldBeEmpty();
    }
}
