using System.ComponentModel.DataAnnotations;

namespace Moeltid.Models;

// CA1716: 'Event' is a reserved keyword in VB/F#, but this is a C#-only project and the name matches the domain model.
#pragma warning disable CA1716
public class Event
#pragma warning restore CA1716
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public required string Slug { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTimeOffset Deadline { get; set; }

    [MaxLength(100)]
    public required string TimeZoneId { get; set; }

    public bool AllowFreeText { get; set; } = true;

    public bool AttendeeOrdersVisible { get; set; } = true;

    public bool IsClosed { get; set; }

    [MaxLength(200)]
    public required string OwnerName { get; set; }

    [MaxLength(200)]
    public required string OwnerEmail { get; set; }

    [MaxLength(100)]
    public required string ManageToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
