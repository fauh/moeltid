namespace Moeltid.Models;

/// <summary>
/// One-per-event scheduled reminder. EventId is the PK so only one reminder can exist per event.
/// </summary>
public class Reminder
{
    /// <summary>Primary key AND foreign key to Event. One reminder per event, enforced at schema level.</summary>
    public Guid EventId { get; set; }

    /// <summary>When the reminder should fire, in UTC.</summary>
    public DateTimeOffset ScheduledFor { get; set; }

    /// <summary>True once the Hangfire job has fired and all emails have been attempted.</summary>
    public bool IsSent { get; set; }

    /// <summary>The Hangfire job ID, used to cancel/reschedule the job.</summary>
    public string? HangfireJobId { get; set; }

    public Event Event { get; set; } = null!;
}
