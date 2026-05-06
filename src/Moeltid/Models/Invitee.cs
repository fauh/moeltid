using System.ComponentModel.DataAnnotations;

namespace Moeltid.Models;

public class Invitee
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }

    [MaxLength(200)]
    public required string Email { get; set; }

    public DateTimeOffset InvitedAt { get; set; }

    public Event Event { get; set; } = null!;
}
