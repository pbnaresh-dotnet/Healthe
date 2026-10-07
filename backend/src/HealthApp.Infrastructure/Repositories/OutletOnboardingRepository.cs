using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Repositories;

public sealed class OutletOnboardingRepository(HealthAppDbContext db) : EfRepository(db), IOutletOnboardingRepository
{
    public Task<OutletOnboardingApplication?> GetAsync(Guid id) => Context.OutletOnboardingApplications.FirstOrDefaultAsync(x => x.Id == id);
    public Task<OutletOnboardingApplication?> GetByUserIdAsync(Guid userId) => Context.OutletOnboardingApplications.FirstOrDefaultAsync(x => x.UserId == userId);
    public async Task<IReadOnlyList<OutletOnboardingApplication>> GetByStatusAsync(string status) =>
        await Context.OutletOnboardingApplications.AsNoTracking().Where(x => x.Status == status).OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
    public async Task AddAsync(OutletOnboardingApplication application)
    {
        Context.OutletOnboardingApplications.Add(application);
        await SaveAsync();
    }
    public async Task UpdateAsync(OutletOnboardingApplication application)
    {
        Context.OutletOnboardingApplications.Update(application);
        await SaveAsync();
    }
}
