using Moeltid.Models;

namespace Moeltid.Services.MealOptions;

public interface IMealOptionService
{
    Task<IReadOnlyList<MealOption>> ListByEventAsync(Guid eventId);
    Task<MealOption> CreateAsync(Guid eventId, string label, MealTag tags);
    Task<MealOption> UpdateAsync(Guid optionId, string label, MealTag tags);
    Task DeleteAsync(Guid optionId);
}
