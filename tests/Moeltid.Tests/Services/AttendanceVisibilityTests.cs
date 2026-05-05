using Moeltid.Models;
using Moeltid.Services;
using Shouldly;

namespace Moeltid.Tests.Services;

public class AttendanceVisibilityTests
{
    private static Attendance MakeAttendance(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        EventId = Guid.NewGuid(),
        Name = "Tester",
        EditToken = "tok",
        OrderType = OrderType.FreeText,
        FreeTextOrder = "An order",
        SubmittedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void Apply_VisibilityOn_ReturnsAllAttendances()
    {
        var attendances = new[] { MakeAttendance(), MakeAttendance(), MakeAttendance() };

        var result = AttendanceVisibility.Apply(
            attendances,
            attendeeOrdersVisible: true,
            myAttendanceId: null);

        result.Count().ShouldBe(3);
    }

    [Fact]
    public void Apply_VisibilityOn_IgnoresMyAttendanceId()
    {
        // When visibility is on, myAttendanceId shouldn't affect the result —
        // everyone sees everyone.
        var attendances = new[] { MakeAttendance(), MakeAttendance() };

        var result = AttendanceVisibility.Apply(
            attendances,
            attendeeOrdersVisible: true,
            myAttendanceId: Guid.NewGuid());

        result.Count().ShouldBe(2);
    }

    [Fact]
    public void Apply_VisibilityOff_WithMyAttendance_ReturnsOnlyMine()
    {
        var mine = MakeAttendance();
        var attendances = new[] { mine, MakeAttendance(), MakeAttendance() };

        var result = AttendanceVisibility.Apply(
            attendances,
            attendeeOrdersVisible: false,
            myAttendanceId: mine.Id);

        var rows = result.ToList();
        rows.Count.ShouldBe(1);
        rows.Single().Id.ShouldBe(mine.Id);
    }

    [Fact]
    public void Apply_VisibilityOff_NoMyAttendance_ReturnsEmpty()
    {
        var attendances = new[] { MakeAttendance(), MakeAttendance() };

        var result = AttendanceVisibility.Apply(
            attendances,
            attendeeOrdersVisible: false,
            myAttendanceId: null);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_VisibilityOff_MyAttendanceNotInList_ReturnsEmpty()
    {
        // Someone holds a token whose attendance was deleted — they should see nothing.
        var attendances = new[] { MakeAttendance(), MakeAttendance() };

        var result = AttendanceVisibility.Apply(
            attendances,
            attendeeOrdersVisible: false,
            myAttendanceId: Guid.NewGuid());

        result.ShouldBeEmpty();
    }
}
