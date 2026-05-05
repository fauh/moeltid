namespace Moeltid.Models;

[Flags]
public enum MealTag
{
    None        = 0,
    Drink       = 1,
    Fish        = 2,
    Vegetarian  = 4,
    Vegan       = 8,
}
