using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moeltid.Models;
using Moeltid.Services;
using Moeltid.Services.Attendances;
using Moeltid.Services.Email;
using Moeltid.Services.Events;
using Moeltid.Services.Invitees;
using Moeltid.Services.MyEvents;
using Moeltid.Services.Reminders;
using Moeltid.Tests.Infrastructure;
using Shouldly;

namespace Moeltid.Tests.Services;

public class MyEventsServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;
    private static readonly IOptions<EmailSettings> DefaultEmailSettings =
        Options.Create(new EmailSettings { BaseUrl = "https://test.example" });

    public MyEventsServiceTests(InMemoryDatabaseFixture db) => _db = db;

    // ── helpers ─────────────────────────────────────────────────────────────

    private MyEventsService BuildSut(IEmailSender? emailSender = null) =>
        new(
            _db.CreateDbContext(),
            new TokenGenerator(),
            new EventService(
                _db.CreateDbContext(),
                new SlugGenerator(new TokenGenerator()),
                new TokenGenerator(),
                new NullEmailSender(),
                DefaultEmailSettings,
                new NullReminderService(),
                NullLogger<EventService>.Instance),
            new AttendanceService(
                _db.CreateDbContext(),
                new TokenGenerator(),
                new NullEmailSender(),
                DefaultEmailSettings,
                NullLogger<AttendanceService>.Instance),
            new InviteeService(
                _db.CreateDbContext(),
                new NullEmailSender(),
                DefaultEmailSettings,
                NullLogger<InviteeService>.Instance),
            emailSender ?? new NullEmailSender(),
            DefaultEmailSettings,
            NullLogger<MyEventsService>.Instance);

    private async Task<Guid> SeedEventAsync(string ownerEmail = "owner@example.com", bool isClosed = false)
    {
        await using var ctx = _db.CreateDbContext();
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Slug = $"my-events-test-{Guid.NewGuid():N}",
            Title = "Test Event",
            StartsAt = DateTimeOffset.UtcNow.AddDays(7),
            Deadline = DateTimeOffset.UtcNow.AddDays(6),
            TimeZoneId = "UTC",
            OwnerName = "Owner",
            OwnerEmail = ownerEmail,
            ManageToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
            IsClosed = isClosed,
        };
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();
        return ev.Id;
    }

    // ── RequestAccessAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RequestAccessAsync_MalformedEmail_Throws()
    {
        var sut = BuildSut();
        await Should.ThrowAsync<ArgumentException>(() => sut.RequestAccessAsync("not-an-email"));
    }

    [Fact]
    public async Task RequestAccessAsync_ValidEmail_PersistsToken()
    {
        var sut = BuildSut();
        await sut.RequestAccessAsync("user@example.com");

        // Allow a moment for fire-and-forget to start (we don't await it, but the token row is written sync)
        await using var ctx = _db.CreateDbContext();
        var token = await ctx.MyEventsAccessTokens
            .FirstOrDefaultAsync(t => t.Email == "user@example.com", default(CancellationToken));

        token.ShouldNotBeNull();
        token.ConsumedAt.ShouldBeNull();
        token.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        token.Token.Length.ShouldBeGreaterThanOrEqualTo(32);
    }

    [Fact]
    public async Task RequestAccessAsync_EmailIsLowercased()
    {
        var sut = BuildSut();
        await sut.RequestAccessAsync("UPPER@EXAMPLE.COM");

        await using var ctx = _db.CreateDbContext();
        var token = ctx.MyEventsAccessTokens.FirstOrDefault(t => t.Email == "upper@example.com");
        token.ShouldNotBeNull();
    }

    // ── ValidateAndConsumeAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ValidateAndConsumeAsync_ValidToken_ReturnsEmailAndMarksConsumed()
    {
        var sut = BuildSut();
        await sut.RequestAccessAsync("consume@example.com");

        await using var ctx = _db.CreateDbContext();
        var row = (await ctx.MyEventsAccessTokens.ToListAsync())
            .First(t => t.Email == "consume@example.com");

        var result = await sut.ValidateAndConsumeAsync(row.Token);

        result.ShouldBe("consume@example.com");

        await using var verifyCtx = _db.CreateDbContext();
        var updated = await verifyCtx.MyEventsAccessTokens.FindAsync(row.Token);
        updated!.ConsumedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_SecondUse_ReturnsNull()
    {
        var sut = BuildSut();
        await sut.RequestAccessAsync("seconduse@example.com");

        await using var ctx = _db.CreateDbContext();
        var row = (await ctx.MyEventsAccessTokens.ToListAsync())
            .First(t => t.Email == "seconduse@example.com");

        await sut.ValidateAndConsumeAsync(row.Token);       // first use — valid
        var second = await sut.ValidateAndConsumeAsync(row.Token); // second use — invalid

        second.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_ExpiredToken_ReturnsNull()
    {
        // Seed an already-expired token directly
        await using var ctx = _db.CreateDbContext();
        var expired = new MyEventsAccessToken
        {
            Token = "expiredtoken123456789012345678901234",
            Email = "expired@example.com",
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1), // in the past
        };
        ctx.MyEventsAccessTokens.Add(expired);
        await ctx.SaveChangesAsync();

        var sut = BuildSut();
        var result = await sut.ValidateAndConsumeAsync(expired.Token);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_UnknownToken_ReturnsNull()
    {
        var sut = BuildSut();
        var result = await sut.ValidateAndConsumeAsync("totallyunknowntoken");
        result.ShouldBeNull();
    }

    // ── GetEventsForEmailAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetEventsForEmailAsync_ReturnsOwnerEvents()
    {
        await SeedEventAsync(ownerEmail: "owneronly@example.com");
        var sut = BuildSut();

        var rows = await sut.GetEventsForEmailAsync("owneronly@example.com");

        rows.Count.ShouldBe(1);
        rows[0].Roles.HasFlag(EventRole.Owner).ShouldBeTrue();
    }
}

// ── test doubles ─────────────────────────────────────────────────────────────

file sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}

file sealed class NullReminderService : IReminderService
{
    public Task<Reminder> ScheduleAsync(Guid eventId, DateTimeOffset whenUtc) =>
        Task.FromResult(new Reminder { EventId = eventId, ScheduledFor = whenUtc });
    public Task CancelAsync(Guid eventId) => Task.CompletedTask;
    public Task<Reminder?> GetByEventAsync(Guid eventId) => Task.FromResult<Reminder?>(null);
}
