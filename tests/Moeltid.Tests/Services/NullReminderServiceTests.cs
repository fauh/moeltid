using Moeltid.Services.Reminders;
using Shouldly;

namespace Moeltid.Tests.Services;

/// <summary>
/// Tests for <see cref="NullReminderService"/> — the no-op implementation used when
/// <c>Reminders:Enabled = false</c> in production.
/// </summary>
public class NullReminderServiceTests
{
    private readonly NullReminderService _sut = new();

    [Fact]
    public async Task ScheduleAsync_ThrowsNotSupportedException()
    {
        var ex = await Should.ThrowAsync<NotSupportedException>(
            () => _sut.ScheduleAsync(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1)));

        ex.Message.ShouldContain("Reminders are disabled");
    }

    [Fact]
    public async Task CancelAsync_DoesNotThrow()
    {
        // Should complete silently — no-op
        await Should.NotThrowAsync(() => _sut.CancelAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByEventAsync_ReturnsNull()
    {
        var result = await _sut.GetByEventAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }
}
