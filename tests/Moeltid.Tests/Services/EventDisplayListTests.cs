using Moeltid.Models;
using Moeltid.Services;
using Shouldly;

namespace Moeltid.Tests.Services;

public class EventDisplayListTests
{
    private static readonly Guid EventId = Guid.NewGuid();

    private static Attendance MakeAttendance(string? email = null, string name = "Alice") => new()
    {
        Id = Guid.NewGuid(),
        EventId = EventId,
        Name = name,
        EditToken = Guid.NewGuid().ToString("N"),
        OrderType = OrderType.FreeText,
        FreeTextOrder = "Pasta",
        SubmittedAt = DateTimeOffset.UtcNow,
        Email = email,
    };

    private static Invitee MakeInvitee(string email) => new()
    {
        Id = Guid.NewGuid(),
        EventId = EventId,
        Email = email.ToLowerInvariant(),
        InvitedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void Build_OrderedOnly_ReturnsOrderedRows()
    {
        var attendances = new[] { MakeAttendance("alice@example.com"), MakeAttendance(null, "Bob") };

        var result = EventDisplayList.Build(attendances, []);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.Kind == DisplayRowKind.Ordered);
    }

    [Fact]
    public void Build_InvitedOnly_ReturnsNoOrderYetRows()
    {
        var invitees = new[] { MakeInvitee("carol@example.com"), MakeInvitee("dave@example.com") };

        var result = EventDisplayList.Build([], invitees);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.Kind == DisplayRowKind.NoOrderYet);
    }

    [Fact]
    public void Build_Mixed_InviteesWithOrderAreExcludedFromNoOrderYetRows()
    {
        var attendances = new[] { MakeAttendance("alice@example.com") };
        var invitees = new[]
        {
            MakeInvitee("alice@example.com"),   // already ordered — should NOT appear as NoOrderYet
            MakeInvitee("bob@example.com"),      // not ordered — should appear as NoOrderYet
        };

        var result = EventDisplayList.Build(attendances, invitees);

        result.Count.ShouldBe(2);
        result.Count(r => r.Kind == DisplayRowKind.Ordered).ShouldBe(1);
        result.Count(r => r.Kind == DisplayRowKind.NoOrderYet).ShouldBe(1);
        result.Single(r => r.Kind == DisplayRowKind.NoOrderYet).Invitee!.Email.ShouldBe("bob@example.com");
    }

    [Fact]
    public void Build_CaseInsensitiveEmailMatch_InviteeWithOrderNotDuplicated()
    {
        // Attendance email is already lower-cased by the value converter, but test
        // the matching logic is case-insensitive regardless.
        var attendances = new[] { MakeAttendance("ALICE@EXAMPLE.COM") };
        var invitees = new[] { MakeInvitee("alice@example.com") };

        var result = EventDisplayList.Build(attendances, invitees);

        // alice@example.com has an order — the invitee row should NOT appear separately
        result.ShouldAllBe(r => r.Kind == DisplayRowKind.Ordered);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Build_EmptyInputs_ReturnsEmptyList()
    {
        var result = EventDisplayList.Build([], []);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_AttendeeWithNoEmail_IncludedAsOrderedRow()
    {
        var attendances = new[] { MakeAttendance(email: null, name: "Anonymous") };
        var invitees = new[] { MakeInvitee("someone@example.com") };

        var result = EventDisplayList.Build(attendances, invitees);

        result.Count.ShouldBe(2);
        result.Count(r => r.Kind == DisplayRowKind.Ordered).ShouldBe(1);
        result.Count(r => r.Kind == DisplayRowKind.NoOrderYet).ShouldBe(1);
    }
}
