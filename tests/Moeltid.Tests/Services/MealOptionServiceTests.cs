using Microsoft.Extensions.Logging.Abstractions;
using Moeltid.Models;
using Moeltid.Services;
using Moeltid.Services.Attendances;
using Moeltid.Services.Email;
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

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsOption()
    {
        var eventId = await SeedEventAsync();

        var option = await _sut.CreateAsync(eventId, "Salmon", MealTag.Fish);

        option.Id.ShouldNotBe(Guid.Empty);
        option.EventId.ShouldBe(eventId);
        option.Label.ShouldBe("Salmon");
        option.Tags.ShouldBe(MealTag.Fish);
    }

    [Fact]
    public async Task UpdateAsync_MutatesLabelAndTags()
    {
        var eventId = await SeedEventAsync();
        var option = await _sut.CreateAsync(eventId, "Old label", MealTag.None);

        var updated = await _sut.UpdateAsync(option.Id, "New label", MealTag.Vegetarian | MealTag.Vegan);

        updated.Label.ShouldBe("New label");
        updated.Tags.ShouldBe(MealTag.Vegetarian | MealTag.Vegan);
    }

    [Fact]
    public async Task DeleteAsync_NoAttendances_RemovesOption()
    {
        var eventId = await SeedEventAsync();
        var option = await _sut.CreateAsync(eventId, "Solo option", MealTag.None);

        await _sut.DeleteAsync(option.Id);

        var remaining = await _sut.ListByEventAsync(eventId);
        remaining.ShouldNotContain(o => o.Id == option.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithDependentAttendances_ConvertsThem()
    {
        // Arrange: create an option and two attendances that reference it
        var eventId = await SeedEventAsync();
        var option = await _sut.CreateAsync(eventId, "Beef burger", MealTag.None);

        var attendanceSvc = new AttendanceService(
            _db.CreateDbContext(),
            new TokenGenerator(),
            new NullEmailSender(),
            Microsoft.Extensions.Options.Options.Create(new Moeltid.Services.Email.EmailSettings { BaseUrl = "https://test.example" }),
            NullLogger<AttendanceService>.Instance);

        var a1 = await attendanceSvc.CreateAsync(new CreateAttendanceRequest(
            eventId, "Alice", null, OrderType.PresetOption, option.Id, null));
        var a2 = await attendanceSvc.CreateAsync(new CreateAttendanceRequest(
            eventId, "Bob", null, OrderType.PresetOption, option.Id, null));

        // Act
        await _sut.DeleteAsync(option.Id);

        // Assert: option gone, attendances converted to FreeText with option label.
        // Use a fresh context to bypass the attendanceSvc identity map (which still tracks
        // the pre-delete entity state).
        var remaining = await _sut.ListByEventAsync(eventId);
        remaining.ShouldNotContain(o => o.Id == option.Id);

        await using var verifyCtx = _db.CreateDbContext();
        var a1After = await verifyCtx.Attendances.FindAsync(a1.Id);
        a1After!.OrderType.ShouldBe(OrderType.FreeText);
        a1After.FreeTextOrder.ShouldBe("Beef burger");
        a1After.MealOptionId.ShouldBeNull();

        var a2After = await verifyCtx.Attendances.FindAsync(a2.Id);
        a2After!.OrderType.ShouldBe(OrderType.FreeText);
        a2After.FreeTextOrder.ShouldBe("Beef burger");
    }

    [Fact]
    public async Task CreateAsync_TagFlagComboRoundTrips()
    {
        var eventId = await SeedEventAsync();
        var tags = MealTag.Drink | MealTag.Vegan;

        var option = await _sut.CreateAsync(eventId, "Vegan smoothie", tags);
        var listed = await _sut.ListByEventAsync(eventId);

        listed.Single(o => o.Id == option.Id).Tags.ShouldBe(tags);
    }
}

file sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}
