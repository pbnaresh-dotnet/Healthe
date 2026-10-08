using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
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


public sealed class PlatformTaxProfileRepository(HealthAppDbContext db) : EfRepository(db), IPlatformTaxProfileRepository
{
    public Task<PlatformTaxProfile?> GetCurrentAsync(DateTime? asOfUtc = null)
    {
        var at = asOfUtc ?? DateTime.UtcNow;
        return Context.PlatformTaxProfiles
            .AsNoTracking()
            .Where(x => x.IsActive &&
                        x.EffectiveFromUtc <= at &&
                        (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > at))
            .OrderByDescending(x => x.EffectiveFromUtc)
            .FirstOrDefaultAsync();
    }
}

public sealed class FinanceTaxRuleRepository(HealthAppDbContext db) : EfRepository(db), IFinanceTaxRuleRepository
{
    public Task<FinanceTaxRule?> GetEffectiveAsync(
        FinanceSupplyType supplyType,
        TaxOperatingMode? taxOperatingMode,
        DateTime asOfUtc)
        => Context.FinanceTaxRules
            .AsNoTracking()
            .Where(x => x.SupplyType == supplyType &&
                        x.IsActive &&
                        x.EffectiveFromUtc <= asOfUtc &&
                        (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > asOfUtc) &&
                        (x.TaxOperatingMode == taxOperatingMode || x.TaxOperatingMode == null))
            .OrderByDescending(x => x.TaxOperatingMode.HasValue)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.IsDefault)
            .FirstOrDefaultAsync();
    public Task<FinanceTaxRule?> GetByIdAsync(Guid id)
        => Context.FinanceTaxRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

}


public sealed class FinanceCalculationSnapshotRepository(HealthAppDbContext db) : EfRepository(db), IFinanceCalculationSnapshotRepository
{
    public Task<FinanceCalculationSnapshot?> GetBySourceAsync(string sourceType, Guid sourceId)
        => Context.FinanceCalculationSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SourceType == sourceType && x.SourceId == sourceId);

    public async Task AddAsync(FinanceCalculationSnapshot snapshot)
    {
        Context.FinanceCalculationSnapshots.Add(snapshot);
        await SaveAsync();
    }
}


public sealed class FinancialDocumentRepository(HealthAppDbContext db) : EfRepository(db), IFinancialDocumentRepository
{
    public Task<FinancialDocument?> GetBySourceAsync(string sourceType, Guid sourceId)
        => Context.FinancialDocuments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SourceType == sourceType && x.SourceId == sourceId);

    public async Task<IReadOnlyList<FinancialDocument>> GetBySourcePrefixAsync(string sourceTypePrefix, Guid sourceId)
        => await Context.FinancialDocuments.AsNoTracking()
            .Where(x => x.SourceType.StartsWith(sourceTypePrefix) && x.SourceId == sourceId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

    public async Task AddAsync(FinancialDocument document)
    {
        Context.FinancialDocuments.Add(document);
        await SaveAsync();
    }

    public async Task AddLineAsync(FinancialDocumentLine line)
    {
        Context.FinancialDocumentLines.Add(line);
        await SaveAsync();
    }

    public async Task AddTaxComponentAsync(FinancialTaxComponent component)
    {
        Context.FinancialTaxComponents.Add(component);
        await SaveAsync();
    }
}
