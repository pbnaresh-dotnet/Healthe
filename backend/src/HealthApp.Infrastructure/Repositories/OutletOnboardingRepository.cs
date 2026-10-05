using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Repositories;

public sealed class OutletOnboardingRepository(HealthAppDbContext db) : EfRepository(db), IOutletOnboardingRepository
{
    public Task<OutletOnboardingApplication?> GetAsync(Guid id) => db.OutletOnboardingApplications.FirstOrDefaultAsync(x => x.Id == id);
    public async Task<IReadOnlyList<OutletOnboardingApplication>> GetByStatusAsync(string status) =>
        await db.OutletOnboardingApplications.AsNoTracking().Where(x => x.Status == status).OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
    public async Task AddAsync(OutletOnboardingApplication application)
    {
        db.OutletOnboardingApplications.Add(application);
        await SaveAsync();
    }
    public async Task UpdateAsync(OutletOnboardingApplication application)
    {
        db.OutletOnboardingApplications.Update(application);
        await SaveAsync();
    }
}
