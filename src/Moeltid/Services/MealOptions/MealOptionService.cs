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
}
