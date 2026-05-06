using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Models;

namespace Moeltid.Services.MealOptions;

public class MealOptionService(AppDbContext db) : IMealOptionService
{
    public async Task<IReadOnlyList<MealOption>> ListByEventAsync(Guid eventId) =>
        await db.MealOptions
            .Where(o => o.EventId == eventId)
            .OrderBy(o => o.Label)
            .ToListAsync();

    public async Task<MealOption> CreateAsync(Guid eventId, string label, MealTag tags)
    {
        var option = new MealOption
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Label = label.Trim(),
            Tags = tags,
        };
        db.MealOptions.Add(option);
        await db.SaveChangesAsync();
        return option;
    }

    public async Task<MealOption> UpdateAsync(Guid optionId, string label, MealTag tags)
    {
        var option = await db.MealOptions.FindAsync(optionId)
            ?? throw new InvalidOperationException($"MealOption {optionId} not found.");

        option.Label = label.Trim();
        option.Tags = tags;
        await db.SaveChangesAsync();
        return option;
    }

    public async Task DeleteAsync(Guid optionId)
    {
        var option = await db.MealOptions.FindAsync(optionId)
            ?? throw new InvalidOperationException($"MealOption {optionId} not found.");

        // Convert dependent attendances to FreeText, preserving the option label as the order text.
        // Done in the same SaveChangesAsync call so it's atomic.
        var dependents = await db.Attendances
            .Where(a => a.MealOptionId == optionId)
            .ToListAsync();

        foreach (var attendance in dependents)
        {
            attendance.OrderType = OrderType.FreeText;
            attendance.FreeTextOrder = option.Label;
            attendance.MealOptionId = null;
        }

        db.MealOptions.Remove(option);
        await db.SaveChangesAsync();
    }
}
