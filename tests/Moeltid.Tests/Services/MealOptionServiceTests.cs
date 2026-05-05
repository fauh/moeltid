using Microsoft.EntityFrameworkCore;
using Moeltid.Models;
using Moeltid.Services.MealOptions;
using Moeltid.Tests.Infrastructure;
using Shouldly;

namespace Moeltid.Tests.Services;

public class MealOptionServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;
    private readonly MealOptionService _sut;

    public MealOptionServiceTests(InMemoryDatabaseFixture db)
    {
        _db = db;
        _sut = new MealOptionService(_db.CreateDbContext());
    }

    private async Task<Guid> SeedEventAsync()
    {
        await using var ctx = _db.CreateDbContext();
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Slug = $"meal-opt-test-{Guid.NewGuid():N}",
            Title = "Test Event",
            StartsAt = DateTimeOffset.UtcNow.AddDays(7),
            Deadline = DateTimeOffset.UtcNow.AddDays(6),
            TimeZoneId = "UTC",
            OwnerName = "Owner",
            OwnerEmail = "owner@example.com",
            ManageToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();
        return ev.Id;
    }

    [Fact]
    public async Task ListByEventAsync_ReturnsOptionsForEvent()
    {
        var eventId = await SeedEventAsync();
        await using var ctx = _db.CreateDbContext();
        ctx.MealOptions.AddRange(
            new MealOption { Id = Guid.NewGuid(), EventId = eventId, Label = "Pasta" },
            new MealOption { Id = Guid.NewGuid(), EventId = eventId, Label = "Salad", Tags = MealTag.Vegetarian }
        );
        await ctx.SaveChangesAsync();

        var result = await _sut.ListByEventAsync(eventId);

        result.ShouldNotBeEmpty();
        result.ShouldAllBe(o => o.EventId == eventId);
    }

    [Fact]
    public async Task ListByEventAsync_UnknownEvent_ReturnsEmpty()
    {
        var result = await _sut.ListByEventAsync(Guid.NewGuid());

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListByEventAsync_DoesNotReturnOptionsFromOtherEvents()
    {
        var eventId1 = await SeedEventAsync();
        var eventId2 = await SeedEventAsync();

        await using var ctx = _db.CreateDbContext();
        ctx.MealOptions.Add(new MealOption { Id = Guid.NewGuid(), EventId = eventId1, Label = "Chicken" });
        await ctx.SaveChangesAsync();

        var result = await _sut.ListByEventAsync(eventId2);

        result.ShouldBeEmpty();
    }
}
