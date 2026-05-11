using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moeltid.Services.Reminders;
using Moeltid.Tests.Infrastructure;
using Shouldly;

namespace Moeltid.Tests.Services;

public class ReminderServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;

    public ReminderServiceTests(InMemoryDatabaseFixture db)
    {
        _db = db;
    }

    private ReminderService BuildSut(IBackgroundJobClient? jobClient = null) =>
        new(_db.CreateDbContext(), jobClient ?? new FakeJobClient(), NullLogger<ReminderService>.Instance);

    private async Task<Guid> SeedEventAsync()
    {
        await using var ctx = _db.CreateDbContext();
        var ev = new Moeltid.Models.Event
        {
            Id = Guid.NewGuid(),
            Slug = $"reminder-test-{Guid.NewGuid():N}",
            Title = "Reminder Test Event",
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

    // ── ScheduleAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_CreatesReminderRow()
    {
        var eventId = await SeedEventAsync();
        var when = DateTimeOffset.UtcNow.AddDays(3);
        var sut = BuildSut();

        var reminder = await sut.ScheduleAsync(eventId, when);

        reminder.EventId.ShouldBe(eventId);
        reminder.ScheduledFor.ShouldBe(when);
        reminder.IsSent.ShouldBeFalse();
        reminder.HangfireJobId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScheduleAsync_SchedulesHangfireJob()
    {
        var eventId = await SeedEventAsync();
        var jobClient = new FakeJobClient();
        var sut = BuildSut(jobClient);

        await sut.ScheduleAsync(eventId, DateTimeOffset.UtcNow.AddDays(2));

        jobClient.ScheduledCount.ShouldBe(1);
    }

    [Fact]
    public async Task ScheduleAsync_WhenReminderAlreadyExists_UpdatesRowAndCancelsOldJob()
    {
        var eventId = await SeedEventAsync();
        var jobClient = new FakeJobClient();
        var sut = BuildSut(jobClient);

        var first = await sut.ScheduleAsync(eventId, DateTimeOffset.UtcNow.AddDays(3));
        var firstJobId = first.HangfireJobId!;
        var whenSecond = DateTimeOffset.UtcNow.AddDays(4);

        await sut.ScheduleAsync(eventId, whenSecond);

        // Old job should be cancelled
        jobClient.DeletedJobIds.ShouldContain(firstJobId);
        jobClient.ScheduledCount.ShouldBe(2);

        // Verify via a fresh context that the row has the new scheduled time
        await using var verifyCtx = _db.CreateDbContext();
        var updated = await verifyCtx.Reminders.FindAsync(eventId);
        updated.ShouldNotBeNull();
        updated!.HangfireJobId.ShouldNotBe(firstJobId);
        updated.ScheduledFor.ShouldBe(whenSecond);
    }

    // ── CancelAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_RemovesRowAndCancelsJob()
    {
        var eventId = await SeedEventAsync();
        var jobClient = new FakeJobClient();
        var sut = BuildSut(jobClient);

        var reminder = await sut.ScheduleAsync(eventId, DateTimeOffset.UtcNow.AddDays(2));
        await sut.CancelAsync(eventId);

        jobClient.DeletedJobIds.ShouldContain(reminder.HangfireJobId!);
        var afterCancel = await sut.GetByEventAsync(eventId);
        afterCancel.ShouldBeNull();
    }

    [Fact]
    public async Task CancelAsync_NoReminder_IsNoOp()
    {
        var eventId = await SeedEventAsync();
        var sut = BuildSut();

        // Should not throw
        await Should.NotThrowAsync(() => sut.CancelAsync(eventId));
    }

    // ── GetByEventAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetByEventAsync_ReturnsNullWhenNoReminder()
    {
        var eventId = await SeedEventAsync();
        var sut = BuildSut();

        var result = await sut.GetByEventAsync(eventId);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByEventAsync_ReturnsReminderAfterSchedule()
    {
        var eventId = await SeedEventAsync();
        var sut = BuildSut();
        var when = DateTimeOffset.UtcNow.AddDays(2);

        await sut.ScheduleAsync(eventId, when);
        var result = await sut.GetByEventAsync(eventId);

        result.ShouldNotBeNull();
        result!.EventId.ShouldBe(eventId);
    }

    // ── On-close cancellation ─────────────────────────────────────────────────

    [Fact]
    public async Task CloseAsync_CancelsPendingReminder()
    {
        var eventId = await SeedEventAsync();
        var jobClient = new FakeJobClient();
        var reminderSvc = new ReminderService(
            _db.CreateDbContext(), jobClient, NullLogger<ReminderService>.Instance);

        var scheduled = await reminderSvc.ScheduleAsync(eventId, DateTimeOffset.UtcNow.AddDays(2));

        // Simulate EventService.CloseAsync calling CancelAsync
        await reminderSvc.CancelAsync(eventId);

        jobClient.DeletedJobIds.ShouldContain(scheduled.HangfireJobId!);
        (await reminderSvc.GetByEventAsync(eventId)).ShouldBeNull();
    }
}

// ── test double ───────────────────────────────────────────────────────────────

internal sealed class FakeJobClient : IBackgroundJobClient
{
    private int _seq;
    public int ScheduledCount { get; private set; }
    public List<string> DeletedJobIds { get; } = [];

    public string Create(Job job, IState state)
    {
        ScheduledCount++;
        return $"fake-job-{++_seq}";
    }

    public bool ChangeState(string jobId, IState state, string? expectedState)
    {
        if (state is DeletedState)
            DeletedJobIds.Add(jobId);
        return true;
    }
}
