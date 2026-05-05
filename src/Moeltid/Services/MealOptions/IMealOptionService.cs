using Moeltid.Models;

namespace Moeltid.Services.MealOptions;

public interface IMealOptionService
{
    Task<IReadOnlyList<MealOption>> ListByEventAsync(Guid eventId);
}
