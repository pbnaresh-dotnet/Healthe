using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Repositories;

public sealed class OutletGroupRepository(HealthAppDbContext db) : IOutletGroupRepository
{
    public async Task<IReadOnlyList<OutletGroup>> GetAllAsync() =>
        await db.OutletGroups.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

    public Task<OutletGroup?> GetAsync(Guid id) =>
        db.OutletGroups.FirstOrDefaultAsync(x => x.Id == id);

    public async Task AddAsync(OutletGroup group)
    {
        db.OutletGroups.Add(group);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(OutletGroup group)
    {
        db.OutletGroups.Update(group);
        await db.SaveChangesAsync();
    }

    public Task<bool> HasOutletsAsync(Guid groupId) =>
        db.Outlets.AnyAsync(x => x.OutletGroupId == groupId);
}
