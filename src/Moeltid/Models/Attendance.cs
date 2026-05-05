namespace Moeltid.Models;

public class Attendance
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public required string EditToken { get; set; }
    public OrderType OrderType { get; set; }
    public Guid? MealOptionId { get; set; }
    public string? FreeTextOrder { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }

    public Event Event { get; set; } = null!;
    public MealOption? MealOption { get; set; }
}
