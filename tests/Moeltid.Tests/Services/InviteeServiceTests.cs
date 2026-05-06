using Microsoft.Extensions.Logging.Abstractions;
using Moeltid.Models;
using Moeltid.Services.Invitees;
using Moeltid.Tests.Infrastructure;
using Shouldly;

namespace Moeltid.Tests.Services;

public class InviteeServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;
    private readonly InviteeService _sut;

    public InviteeServiceTests(InMemoryDatabaseFixture db)
    {
        _db = db;
        _sut = new InviteeService(
            _db.CreateDbContext(),
            new NullEmailSender(),
            NullLogger<InviteeService>.Instance);
    }

    // ── seed helpers ────────────────────────────────────────────────────────

    private async Task<Guid> SeedEventAsync()
    {
        await using var ctx = _db.CreateDbContext();
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Slug = $"inv-test-{Guid.NewGuid():N}",
            Title = "Invitee Test Event",
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

    private async Task<Guid> SeedAttendanceAsync(Guid eventId, string email)
    {
        await using var ctx = _db.CreateDbContext();
        var a = new Attendance
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = "Attendee",
            Email = email.ToLowerInvariant(),
            EditToken = Guid.NewGuid().ToString("N"),
            OrderType = OrderType.FreeText,
            FreeTextOrder = "Anything",
            SubmittedAt = DateTimeOffset.UtcNow,
        };
        ctx.Attendances.Add(a);
        await ctx.SaveChangesAsync();
        return a.Id;
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_LowerCasesEmail()
    {
        var eventId = await SeedEventAsync();

        var invitee = await _sut.CreateAsync(eventId, "Alice@EXAMPLE.COM");

        invitee.Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task CreateAsync_DuplicateInvitee_Throws()
    {
        var eventId = await SeedEventAsync();
        await _sut.CreateAsync(eventId, "alice@example.com");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.CreateAsync(eventId, "alice@example.com"));
    }

    [Fact]
    public async Task CreateAsync_EmailAlreadyHasAttendance_Throws()
    {
        var eventId = await SeedEventAsync();
        await SeedAttendanceAsync(eventId, "bob@example.com");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.CreateAsync(eventId, "bob@example.com"));
    }

    // ── CreateBatchAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBatchAsync_DeduplicatesWithinBatch()
    {
        var eventId = await SeedEventAsync();

        var created = await _sut.CreateBatchAsync(eventId,
            ["carol@example.com", "CAROL@EXAMPLE.COM", "dave@example.com"]);

        // carol@example.com appears twice — only one should be created
        created.Count.ShouldBe(2);
        created.Select(i => i.Email).ShouldBe(["carol@example.com", "dave@example.com"], ignoreOrder: true);
    }

    [Fact]
    public async Task CreateBatchAsync_SkipsExistingInviteesAndAttendances()
    {
        var eventId = await SeedEventAsync();
        await _sut.CreateAsync(eventId, "existing@example.com");
        await SeedAttendanceAsync(eventId, "ordered@example.com");

        var created = await _sut.CreateBatchAsync(eventId,
            ["existing@example.com", "ordered@example.com", "new@example.com"]);

        created.Count.ShouldBe(1);
        created[0].Email.ShouldBe("new@example.com");
    }

    [Fact]
    public async Task CreateBatchAsync_EmptyInput_ReturnsEmpty()
    {
        var eventId = await SeedEventAsync();

        var created = await _sut.CreateBatchAsync(eventId, []);

        created.ShouldBeEmpty();
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_KnownId_ReturnsInvitee()
    {
        var eventId = await SeedEventAsync();
        var inv = await _sut.CreateAsync(eventId, "find@example.com");

        var found = await _sut.GetByIdAsync(inv.Id);

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(inv.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    // ── ListUnorderedByEventAsync ────────────────────────────────────────────

    [Fact]
    public async Task ListUnorderedByEventAsync_ExcludesInviteesWithAttendances()
    {
        var eventId = await SeedEventAsync();
        var invA = await _sut.CreateAsync(eventId, "ordered@example.com");
        var invB = await _sut.CreateAsync(eventId, "unordered@example.com");
        await SeedAttendanceAsync(eventId, invA.Email);

        var unordered = await _sut.ListUnorderedByEventAsync(eventId);

        unordered.Count.ShouldBe(1);
        unordered[0].Email.ShouldBe(invB.Email);
    }

    [Fact]
    public async Task ListUnorderedByEventAsync_AllOrderedInvitees_ReturnsEmpty()
    {
        var eventId = await SeedEventAsync();
        var inv = await _sut.CreateAsync(eventId, "all-ordered@example.com");
        await SeedAttendanceAsync(eventId, inv.Email);

        var unordered = await _sut.ListUnorderedByEventAsync(eventId);

        unordered.ShouldBeEmpty();
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_KeepAttendance_RemovesOnlyInvitee()
    {
        var eventId = await SeedEventAsync();
        var inv = await _sut.CreateAsync(eventId, "keep@example.com");
        var attId = await SeedAttendanceAsync(eventId, inv.Email);

        await _sut.DeleteAsync(inv.Id, alsoDeleteMatchingAttendance: false);

        var invAfter = await _sut.GetByIdAsync(inv.Id);
        invAfter.ShouldBeNull();

        await using var ctx = _db.CreateDbContext();
        var attAfter = await ctx.Attendances.FindAsync(attId);
        attAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_AlsoDeleteAttendance_RemovesBoth()
    {
        var eventId = await SeedEventAsync();
        var inv = await _sut.CreateAsync(eventId, "remove-both@example.com");
        var attId = await SeedAttendanceAsync(eventId, inv.Email);

        await _sut.DeleteAsync(inv.Id, alsoDeleteMatchingAttendance: true);

        var invAfter = await _sut.GetByIdAsync(inv.Id);
        invAfter.ShouldBeNull();

        await using var ctx = _db.CreateDbContext();
        var attAfter = await ctx.Attendances.FindAsync(attId);
        attAfter.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.DeleteAsync(Guid.NewGuid(), false));
    }

    // ── SendRemindersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SendRemindersAsync_CallsSendOncePerUnorderedInvitee()
    {
        var eventId = await SeedEventAsync();
        var inv1 = await _sut.CreateAsync(eventId, "remind1@example.com");
        var inv2 = await _sut.CreateAsync(eventId, "remind2@example.com");
        var inv3 = await _sut.CreateAsync(eventId, "already-ordered@example.com");
        await SeedAttendanceAsync(eventId, inv3.Email);

        var recorder = new RecordingEmailSender();
        var svc = new InviteeService(_db.CreateDbContext(), recorder, NullLogger<InviteeService>.Instance);

        await svc.SendRemindersAsync(eventId);

        recorder.Sent.Count.ShouldBe(2);
        recorder.Sent.Select(m => m.To).ShouldBe(
            [inv1.Email, inv2.Email], ignoreOrder: true);
    }

    [Fact]
    public async Task SendRemindersAsync_AllOrdered_SendsNothing()
    {
        var eventId = await SeedEventAsync();
        var inv = await _sut.CreateAsync(eventId, "all-done@example.com");
        await SeedAttendanceAsync(eventId, inv.Email);

        var recorder = new RecordingEmailSender();
        var svc = new InviteeService(_db.CreateDbContext(), recorder, NullLogger<InviteeService>.Instance);

        await svc.SendRemindersAsync(eventId);

        recorder.Sent.ShouldBeEmpty();
    }
}

// ── test doubles ────────────────────────────────────────────────────────────

file sealed class NullEmailSender : Moeltid.Services.Email.IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}

file sealed class RecordingEmailSender : Moeltid.Services.Email.IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string body)
    {
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}
