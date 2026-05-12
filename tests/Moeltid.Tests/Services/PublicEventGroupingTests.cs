using Moeltid.Models;
using Moeltid.Services.Events;
using Shouldly;

namespace Moeltid.Tests.Services;

/// <summary>
/// Tests for PublicEventGrouping — pure helper, no DB needed.
/// </summary>
public class PublicEventGroupingTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static EventListRow MakeRow(bool isOngoing, int daysOffset = 0) =>
        new(
            Event: new Event
            {
                Id = Guid.NewGuid(),
                Slug = $"test-{Guid.NewGuid():N}",
                Title = "Test Event",
                StartsAt = DateTimeOffset.UtcNow.AddDays(daysOffset),
                Deadline = DateTimeOffset.UtcNow.AddDays(daysOffset - 1),
                TimeZoneId = "UTC",
                OwnerName = "Owner",
                OwnerEmail = "owner@example.com",
                ManageToken = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            OrderedCount: 0,
            IsOngoing: isOngoing);

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_EmptyList_ReturnsBothEmpty()
    {
        var result = PublicEventGrouping.Build([]);
        result.Ongoing.ShouldBeEmpty();
        result.Past.ShouldBeEmpty();
    }

    [Fact]
    public void Build_AllOngoing_PastIsEmpty()
    {
        var rows = new[] { MakeRow(true, 2), MakeRow(true, 5) };
        var result = PublicEventGrouping.Build(rows);

        result.Ongoing.Count.ShouldBe(2);
        result.Past.ShouldBeEmpty();
    }

    [Fact]
    public void Build_AllPast_OngoingIsEmpty()
    {
        var rows = new[] { MakeRow(false, -2), MakeRow(false, -5) };
        var result = PublicEventGrouping.Build(rows);

        result.Ongoing.ShouldBeEmpty();
        result.Past.Count.ShouldBe(2);
    }

    [Fact]
    public void Build_OngoingOrderedAscending()
    {
        var soon = MakeRow(true, daysOffset: 2);
        var later = MakeRow(true, daysOffset: 10);
        // Pass in reverse order to confirm sorting, not just passthrough
        var result = PublicEventGrouping.Build([later, soon]);

        result.Ongoing[0].Event.StartsAt.ShouldBeLessThan(result.Ongoing[1].Event.StartsAt);
    }

    [Fact]
    public void Build_PastOrderedDescending()
    {
        var recent = MakeRow(false, daysOffset: -1);
        var older = MakeRow(false, daysOffset: -10);
        // Pass older first to confirm sorting
        var result = PublicEventGrouping.Build([older, recent]);

        result.Past[0].Event.StartsAt.ShouldBeGreaterThan(result.Past[1].Event.StartsAt);
    }

    [Fact]
    public void Build_MixedList_SplitsCorrectly()
    {
        var rows = new[]
        {
            MakeRow(true, 3),
            MakeRow(false, -2),
            MakeRow(true, 7),
            MakeRow(false, -5),
        };
        var result = PublicEventGrouping.Build(rows);

        result.Ongoing.Count.ShouldBe(2);
        result.Past.Count.ShouldBe(2);
        result.Ongoing.ShouldAllBe(r => r.IsOngoing);
        result.Past.ShouldAllBe(r => !r.IsOngoing);
    }
}
