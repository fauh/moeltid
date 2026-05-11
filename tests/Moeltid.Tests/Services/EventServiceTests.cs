using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moeltid.Models;
using Moeltid.Services;
using Moeltid.Services.Email;
using Moeltid.Services.Events;
using Moeltid.Services.Reminders;
using Moeltid.Tests.Infrastructure;
using Shouldly;

namespace Moeltid.Tests.Services;

public class EventServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;
    private readonly EventService _sut;
    private static readonly IOptions<EmailSettings> DefaultEmailSettings =
        Options.Create(new EmailSettings { BaseUrl = "https://test.example" });

    public EventServiceTests(InMemoryDatabaseFixture db)
    {
        _db = db;
        _sut = new EventService(
            _db.CreateDbContext(),
            new SlugGenerator(new TokenGenerator()),
            new TokenGenerator(),
            new NullEmailSender(),
            DefaultEmailSettings,
            new NullReminderService(),
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

        ev.Id.ShouldNotBe(Guid.Empty);
        ev.Title.ShouldBe("Team Lunch");
        ev.Description.ShouldBe("Bring your appetite");
        ev.OwnerName.ShouldBe("Alice");
        ev.ManageToken.Length.ShouldBe(22);
        ev.Slug.ShouldMatch(@"^team-lunch-[A-Za-z0-9_-]{6}$");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_LowerCasesOwnerEmail()
    {
        var ev = await _sut.CreateAsync(MakeRequest(ownerEmail: "Owner@Example.COM"));

        ev.OwnerEmail.ShouldBe("owner@example.com");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_TrimsWhitespaceFromDescription()
    {
        var ev = await _sut.CreateAsync(MakeRequest(description: "  padded  "));

        ev.Description.ShouldBe("padded");
    }

    [Fact]
    public async Task CreateAsync_NullDescription_StoresNull()
    {
        var ev = await _sut.CreateAsync(MakeRequest(description: null));

        ev.Description.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_UtcTimeZone_StoresStaratsAtAsUtc()
    {
        var wallClock = new DateTime(2026, 6, 15, 12, 0, 0);
        var request = MakeRequest(timeZoneId: "UTC") with { StartsAt = wallClock };

        var ev = await _sut.CreateAsync(request);

        ev.StartsAt.ShouldBe(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task CreateAsync_StockholmSummerTimeZone_ConvertsStartsAtToUtc()
    {
        // Stockholm is UTC+2 in summer — 12:00 local → 10:00 UTC
        var wallClock = new DateTime(2026, 6, 15, 12, 0, 0);
        var request = MakeRequest(timeZoneId: "Europe/Stockholm") with { StartsAt = wallClock };

        var ev = await _sut.CreateAsync(request);

        ev.StartsAt.UtcDateTime.ShouldBe(new DateTime(2026, 6, 15, 10, 0, 0));
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_KnownSlug_ReturnsEvent()
    {
        var created = await _sut.CreateAsync(MakeRequest(title: "Slug Lookup Test"));

        var found = await _sut.GetBySlugAsync(created.Slug);

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task GetBySlugAsync_UnknownSlug_ReturnsNull()
    {
        var result = await _sut.GetBySlugAsync("no-such-slug-xyz");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_KnownId_ReturnsEvent()
    {
        var created = await _sut.CreateAsync(MakeRequest(title: "Id Lookup Test"));

        var found = await _sut.GetByIdAsync(created.Id);

        found.ShouldNotBeNull();
        found!.Slug.ShouldBe(created.Slug);
    }

    // ── manage methods ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ChangesFields()
    {
        var created = await _sut.CreateAsync(MakeRequest(title: "Original"));

        var updated = await _sut.UpdateAsync(created.Id, new UpdateEventRequest(
            Title: "Updated Title",
            Description: "New desc",
            StartsAt: new DateTime(2026, 7, 1, 12, 0, 0),
            Deadline: new DateTime(2026, 6, 30, 12, 0, 0),
            TimeZoneId: "UTC",
            AllowFreeText: false,
            AttendeeOrdersVisible: false));

        updated.Title.ShouldBe("Updated Title");
        updated.Description.ShouldBe("New desc");
        updated.AllowFreeText.ShouldBeFalse();
        updated.AttendeeOrdersVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task CloseAsync_SetsIsClosed()
    {
        var created = await _sut.CreateAsync(MakeRequest(title: "Event To Close"));
        created.IsClosed.ShouldBeFalse();

        var closed = await _sut.CloseAsync(created.Id);

        closed.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public async Task RotateManageTokenAsync_ReturnsNewToken()
    {
        var created = await _sut.CreateAsync(MakeRequest(title: "Token Rotation"));
        var originalToken = created.ManageToken;

        var newToken = await _sut.RotateManageTokenAsync(created.Id);

        newToken.ShouldNotBe(originalToken);
        newToken.Length.ShouldBe(22);
    }

    [Fact]
    public async Task RotateManageTokenAsync_OldTokenNoLongerOnEvent()
    {
        var created = await _sut.CreateAsync(MakeRequest(title: "Token Rotation Check"));
        var originalToken = created.ManageToken;

        await _sut.RotateManageTokenAsync(created.Id);
        var reloaded = await _sut.GetByIdAsync(created.Id);

        reloaded!.ManageToken.ShouldNotBe(originalToken);
    }

    [Fact]
    public async Task GetByOwnerEmailAsync_ReturnsMatchingEvents()
    {
        await _sut.CreateAsync(MakeRequest(title: "Email Match 1", ownerEmail: "find-me@example.com"));
        await _sut.CreateAsync(MakeRequest(title: "Email Match 2", ownerEmail: "FIND-ME@EXAMPLE.COM"));
        await _sut.CreateAsync(MakeRequest(title: "Other owner", ownerEmail: "other@example.com"));

        var results = await _sut.GetByOwnerEmailAsync("find-me@example.com");

        results.ShouldAllBe(e => e.OwnerEmail == "find-me@example.com");
        results.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetByOwnerEmailAsync_UnknownEmail_ReturnsEmpty()
    {
        var results = await _sut.GetByOwnerEmailAsync("nobody@nowhere.invalid");

        results.ShouldBeEmpty();
    }

    // ── create with meal options + invitees ──────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithMealOptions_PersistsMealOptions()
    {
        var request = MakeRequest(title: "Options Event") with
        {
            MealOptions =
            [
                new MealOptionDraft("Salmon", MealTag.Fish),
                new MealOptionDraft("Tofu stir-fry", MealTag.Vegetarian | MealTag.Vegan),
            ],
        };

        var ev = await _sut.CreateAsync(request);

        await using var ctx = _db.CreateDbContext();
        var options = await ctx.MealOptions.Where(o => o.EventId == ev.Id).ToListAsync();
        options.Count.ShouldBe(2);
        options.ShouldContain(o => o.Label == "Salmon" && o.Tags == MealTag.Fish);
        options.ShouldContain(o => o.Label == "Tofu stir-fry" && o.Tags == (MealTag.Vegetarian | MealTag.Vegan));
    }

    [Fact]
    public async Task CreateAsync_WithInvitees_PersistsLowerCasedInvitees()
    {
        var request = MakeRequest(title: "Invitee Event") with
        {
            InviteeEmails = ["Bob@Example.COM", "carol@example.com"],
        };

        var ev = await _sut.CreateAsync(request);

        await using var ctx = _db.CreateDbContext();
        var invitees = await ctx.Invitees.Where(i => i.EventId == ev.Id).ToListAsync();
        invitees.Count.ShouldBe(2);
        invitees.ShouldContain(i => i.Email == "bob@example.com");
        invitees.ShouldContain(i => i.Email == "carol@example.com");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateInviteeEmails_DeduplicatesSilently()
    {
        var request = MakeRequest(title: "Dedup Invitees") with
        {
            InviteeEmails = ["dup@example.com", "DUP@EXAMPLE.COM", "unique@example.com"],
        };

        var ev = await _sut.CreateAsync(request);

        await using var ctx = _db.CreateDbContext();
        var invitees = await ctx.Invitees.Where(i => i.EventId == ev.Id).ToListAsync();
        invitees.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateAsync_EmptyOptionsAndInvitees_StillCreatesEvent()
    {
        var request = MakeRequest(title: "Empty Extras") with
        {
            MealOptions = [],
            InviteeEmails = [],
        };

        var ev = await _sut.CreateAsync(request);

        ev.Id.ShouldNotBe(Guid.Empty);
        await using var ctx = _db.CreateDbContext();
        (await ctx.MealOptions.AnyAsync(o => o.EventId == ev.Id)).ShouldBeFalse();
        (await ctx.Invitees.AnyAsync(i => i.EventId == ev.Id)).ShouldBeFalse();
    }
}

file sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}

file sealed class NullReminderService : IReminderService
{
    public Task<Moeltid.Models.Reminder> ScheduleAsync(Guid eventId, DateTimeOffset whenUtc) =>
        Task.FromResult(new Moeltid.Models.Reminder { EventId = eventId, ScheduledFor = whenUtc });
    public Task CancelAsync(Guid eventId) => Task.CompletedTask;
    public Task<Moeltid.Models.Reminder?> GetByEventAsync(Guid eventId) =>
        Task.FromResult<Moeltid.Models.Reminder?>(null);
}
