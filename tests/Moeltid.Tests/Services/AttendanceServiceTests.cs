using Microsoft.Extensions.Logging.Abstractions;
using Moeltid.Models;
using Moeltid.Services;
using Moeltid.Services.Attendances;
using Moeltid.Services.Email;
using Moeltid.Tests.Infrastructure;
using Shouldly;

namespace Moeltid.Tests.Services;

public class AttendanceServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;
    private readonly AttendanceService _sut;

    public AttendanceServiceTests(InMemoryDatabaseFixture db)
    {
        _db = db;
        _sut = new AttendanceService(
            _db.CreateDbContext(),
            new TokenGenerator(),
            new NullEmailSender(),
            NullLogger<AttendanceService>.Instance);
    }

    // ── seed helpers ────────────────────────────────────────────────────────

    private async Task<Guid> SeedEventAsync()
    {
        await using var ctx = _db.CreateDbContext();
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Slug = $"att-test-{Guid.NewGuid():N}",
            Title = "Attendance Test Event",
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

    private async Task<Guid> SeedMealOptionAsync(Guid eventId, string label = "Pasta")
    {
        await using var ctx = _db.CreateDbContext();
        var option = new MealOption { Id = Guid.NewGuid(), EventId = eventId, Label = label };
        ctx.MealOptions.Add(option);
        await ctx.SaveChangesAsync();
        return option.Id;
    }

    private static CreateAttendanceRequest FreeTextRequest(Guid eventId, string name = "Alice") =>
        new(EventId: eventId, Name: name, Email: null,
            OrderType: OrderType.FreeText, MealOptionId: null, FreeTextOrder: "No pickles please");

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_FreeTextOrder_PersistsAttendance()
    {
        var eventId = await SeedEventAsync();

        var attendance = await _sut.CreateAsync(FreeTextRequest(eventId));

        attendance.Id.ShouldNotBe(Guid.Empty);
        attendance.EventId.ShouldBe(eventId);
        attendance.Name.ShouldBe("Alice");
        attendance.OrderType.ShouldBe(OrderType.FreeText);
        attendance.FreeTextOrder.ShouldBe("No pickles please");
        attendance.MealOptionId.ShouldBeNull();
        attendance.EditToken.Length.ShouldBe(22);
    }

    [Fact]
    public async Task CreateAsync_PresetOptionOrder_PersistsAttendance()
    {
        var eventId = await SeedEventAsync();
        var optionId = await SeedMealOptionAsync(eventId);

        var request = new CreateAttendanceRequest(
            EventId: eventId, Name: "Bob", Email: null,
            OrderType: OrderType.PresetOption, MealOptionId: optionId, FreeTextOrder: null);

        var attendance = await _sut.CreateAsync(request);

        attendance.OrderType.ShouldBe(OrderType.PresetOption);
        attendance.MealOptionId.ShouldBe(optionId);
        attendance.FreeTextOrder.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_EmailProvided_LowerCasesEmail()
    {
        var eventId = await SeedEventAsync();
        var request = FreeTextRequest(eventId) with { Email = "Alice@Example.COM" };

        var attendance = await _sut.CreateAsync(request);

        attendance.Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task CreateAsync_FreeTextOrder_MealOptionIdNotStored()
    {
        // MealOptionId should be null even if accidentally provided for a FreeText order
        var eventId = await SeedEventAsync();
        var optionId = await SeedMealOptionAsync(eventId);
        var request = new CreateAttendanceRequest(
            EventId: eventId, Name: "Carol", Email: null,
            OrderType: OrderType.FreeText, MealOptionId: optionId, FreeTextOrder: "Vegan please");

        var attendance = await _sut.CreateAsync(request);

        attendance.MealOptionId.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_FreeTextWithNoText_Throws()
    {
        var eventId = await SeedEventAsync();
        var request = new CreateAttendanceRequest(
            EventId: eventId, Name: "Dave", Email: null,
            OrderType: OrderType.FreeText, MealOptionId: null, FreeTextOrder: null);

        await Should.ThrowAsync<ArgumentException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_PresetOptionWithNoOptionId_Throws()
    {
        var eventId = await SeedEventAsync();
        var request = new CreateAttendanceRequest(
            EventId: eventId, Name: "Eve", Email: null,
            OrderType: OrderType.PresetOption, MealOptionId: null, FreeTextOrder: null);

        await Should.ThrowAsync<ArgumentException>(() => _sut.CreateAsync(request));
    }

    // ── GetByEditTokenAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByEditTokenAsync_ValidToken_ReturnsAttendance()
    {
        var eventId = await SeedEventAsync();
        var created = await _sut.CreateAsync(FreeTextRequest(eventId));

        var found = await _sut.GetByEditTokenAsync(created.EditToken);

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task GetByEditTokenAsync_UnknownToken_ReturnsNull()
    {
        var result = await _sut.GetByEditTokenAsync("not-a-real-token");

        result.ShouldBeNull();
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ChangesOrder()
    {
        var eventId = await SeedEventAsync();
        var created = await _sut.CreateAsync(FreeTextRequest(eventId));

        var updated = await _sut.UpdateAsync(created.Id, new UpdateAttendanceRequest(
            OrderType: OrderType.FreeText,
            MealOptionId: null,
            FreeTextOrder: "Changed my mind — fish please"));

        updated.FreeTextOrder.ShouldBe("Changed my mind — fish please");
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.UpdateAsync(Guid.NewGuid(), new UpdateAttendanceRequest(
                OrderType.FreeText, null, "Anything")));
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesAttendance()
    {
        var eventId = await SeedEventAsync();
        var created = await _sut.CreateAsync(FreeTextRequest(eventId));

        await _sut.DeleteAsync(created.Id);

        var after = await _sut.GetByEditTokenAsync(created.EditToken);
        after.ShouldBeNull();
    }

    // ── DeleteByOwnerAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteByOwnerAsync_RemovesAttendance()
    {
        var eventId = await SeedEventAsync();
        var created = await _sut.CreateAsync(FreeTextRequest(eventId));

        await _sut.DeleteByOwnerAsync(created.Id);

        var after = await _sut.GetByEditTokenAsync(created.EditToken);
        after.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByOwnerAsync_UnknownId_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.DeleteByOwnerAsync(Guid.NewGuid()));
    }

    // ── ListByEventAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListByEventAsync_ReturnsOnlyEventAttendances()
    {
        var eventId1 = await SeedEventAsync();
        var eventId2 = await SeedEventAsync();
        await _sut.CreateAsync(FreeTextRequest(eventId1, "A"));
        await _sut.CreateAsync(FreeTextRequest(eventId1, "B"));
        await _sut.CreateAsync(FreeTextRequest(eventId2, "C"));

        var list = await _sut.ListByEventAsync(eventId1);

        list.Count.ShouldBe(2);
        list.ShouldAllBe(a => a.EventId == eventId1);
    }
}

file sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}
