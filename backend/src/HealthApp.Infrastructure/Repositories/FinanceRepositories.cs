using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Repositories;

public sealed class OutletTaxProfileRepository(HealthAppDbContext db) : EfRepository(db), IOutletTaxProfileRepository
{
    public Task<OutletTaxProfile?> GetCurrentAsync(Guid outletId, DateTime? asOfUtc = null)
    {
        var at = asOfUtc ?? DateTime.UtcNow;
        return Context.OutletTaxProfiles
            .AsNoTracking()
            .Where(x => x.OutletId == outletId &&
                        x.EffectiveFromUtc <= at &&
                        (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > at))
            .OrderByDescending(x => x.EffectiveFromUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<OutletTaxProfile>> GetHistoryAsync(Guid outletId) =>
        await Context.OutletTaxProfiles
            .AsNoTracking()
            .Where(x => x.OutletId == outletId)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ToListAsync();

    public async Task AddVersionAsync(OutletTaxProfile profile, DateTime effectiveFromUtc)
    {
        var current = await Context.OutletTaxProfiles
            .Where(x => x.OutletId == profile.OutletId &&
                        x.EffectiveFromUtc < effectiveFromUtc &&
                        (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > effectiveFromUtc))
            .OrderByDescending(x => x.EffectiveFromUtc)
            .FirstOrDefaultAsync();

        if (current is not null)
        {
            current.EffectiveToUtc = effectiveFromUtc;
            current.IsActive = false;
        }

        profile.Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id;
        profile.EffectiveFromUtc = effectiveFromUtc;
        profile.EffectiveToUtc = null;
        profile.IsActive = true;
        profile.CreatedAtUtc = DateTime.UtcNow;

        Context.OutletTaxProfiles.Add(profile);
        await SaveAsync();
    }
}

public sealed class FinancePolicyRepository(HealthAppDbContext db) : EfRepository(db), IFinancePolicyRepository
{
    public Task<FinancePolicyDocument?> GetDocumentAsync(string code)
        => Context.FinancePolicyDocuments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code && x.IsActive);

    public async Task<IReadOnlyList<FinancePolicyDocumentVersion>> GetVersionsAsync(Guid documentId)
        => await Context.FinancePolicyDocumentVersions.AsNoTracking()
            .Where(x => x.FinancePolicyDocumentId == documentId)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<FinancePolicyDocumentSection>> GetSectionsAsync(Guid versionId)
        => await Context.FinancePolicyDocumentSections.AsNoTracking()
            .Where(x => x.FinancePolicyDocumentVersionId == versionId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.SectionCode)
            .ToListAsync();
}
