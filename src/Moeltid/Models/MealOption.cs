namespace Moeltid.Models;

public class MealOption
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public required string Label { get; set; }
    public MealTag Tags { get; set; } = MealTag.None;

    public Event Event { get; set; } = null!;
}
