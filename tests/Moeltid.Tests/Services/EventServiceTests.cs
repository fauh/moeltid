using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moeltid.Services;
using Moeltid.Services.Email;
using Moeltid.Services.Events;
using Moeltid.Tests.Infrastructure;

namespace Moeltid.Tests.Services;

public class EventServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;
    private readonly EventService _sut;

    public EventServiceTests(InMemoryDatabaseFixture db)
    {
        _db = db;
        _sut = new EventService(
            _db.CreateDbContext(),
            new SlugGenerator(new TokenGenerator()),
            new TokenGenerator(),
            new NullEmailSender(),
            NullLogger<EventService>.Instance);
    }

    private static CreateEventRequest MakeRequest(
        string title = "Team Lunch",
        string? description = "Bring your appetite",
        string timeZoneId = "UTC",
        string ownerEmail = "Owner@Example.COM") =>
        new(
            Title: title,
            Description: description,
            StartsAt: new DateTime(2026, 6, 15, 12, 0, 0),
            Deadline: new DateTime(2026, 6, 14, 12, 0, 0),
            TimeZoneId: timeZoneId,
            OwnerName: "Alice",
            OwnerEmail: ownerEmail,
            AllowFreeText: true,
            AttendeeOrdersVisible: true
        );

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsEventWithCorrectFields()
    {
        var ev = await _sut.CreateAsync(MakeRequest());

        ev.Id.Should().NotBeEmpty();
        ev.Title.Should().Be("Team Lunch");
        ev.Description.Should().Be("Bring your appetite");
        ev.OwnerName.Should().Be("Alice");
        ev.ManageToken.Should().HaveLength(22);
        ev.Slug.Should().MatchRegex(@"^team-lunch-[A-Za-z0-9_-]{6}$");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_LowerCasesOwnerEmail()
    {
        var ev = await _sut.CreateAsync(MakeRequest(ownerEmail: "Owner@Example.COM"));

        ev.OwnerEmail.Should().Be("owner@example.com");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_TrimsWhitespaceFromDescription()
    {
        var ev = await _sut.CreateAsync(MakeRequest(description: "  padded  "));

        ev.Description.Should().Be("padded");
    }

    [Fact]
    public async Task CreateAsync_NullDescription_StoresNull()
    {
        var ev = await _sut.CreateAsync(MakeRequest(description: null));

        ev.Description.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_UtcTimeZone_StoresStaratsAtAsUtc()
    {
        var wallClock = new DateTime(2026, 6, 15, 12, 0, 0);
        var request = MakeRequest(timeZoneId: "UTC") with { StartsAt = wallClock };

        var ev = await _sut.CreateAsync(request);

        ev.StartsAt.Should().Be(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task CreateAsync_StockholmSummerTimeZone_ConvertsStartsAtToUtc()
    {
        // Stockholm is UTC+2 in summer — 12:00 local → 10:00 UTC
        var wallClock = new DateTime(2026, 6, 15, 12, 0, 0);
        var request = MakeRequest(timeZoneId: "Europe/Stockholm") with { StartsAt = wallClock };

        var ev = await _sut.CreateAsync(request);

        ev.StartsAt.UtcDateTime.Should().Be(new DateTime(2026, 6, 15, 10, 0, 0));
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_KnownSlug_ReturnsEvent()
    {
        var created = await _sut.CreateAsync(MakeRequest(title: "Slug Lookup Test"));

        var found = await _sut.GetBySlugAsync(created.Slug);

        found.Should().NotBeNull();
        found!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetBySlugAsync_UnknownSlug_ReturnsNull()
    {
        var result = await _sut.GetBySlugAsync("no-such-slug-xyz");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_KnownId_ReturnsEvent()
    {
        var created = await _sut.CreateAsync(MakeRequest(title: "Id Lookup Test"));

        var found = await _sut.GetByIdAsync(created.Id);

        found.Should().NotBeNull();
        found!.Slug.Should().Be(created.Slug);
    }
}

file sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}
