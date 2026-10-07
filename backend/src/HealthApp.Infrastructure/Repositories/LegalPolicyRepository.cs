using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Repositories;

public sealed class OutletLegalPolicyRepository(HealthAppDbContext db) : EfRepository(db), IOutletLegalPolicyRepository
{
    public Task<OutletLegalPolicyVersion?> GetPublishedAsync(Guid outletId)
        => Context.OutletLegalPolicyVersions.AsNoTracking()
            .Where(x => x.OutletId == outletId && x.IsPublished)
            .OrderByDescending(x => x.PublishedAtUtc)
            .FirstOrDefaultAsync();

    public Task<OutletLegalPolicyVersion?> GetByIdAsync(Guid id, Guid outletId)
        => Context.OutletLegalPolicyVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OutletId == outletId);

    public Task<OutletLegalPolicyVersion?> GetByVersionAsync(Guid outletId, string version)
        => Context.OutletLegalPolicyVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OutletId == outletId && x.Version == version.Trim());

    public async Task<IReadOnlyList<OutletLegalPolicyVersion>> GetHistoryAsync(Guid outletId)
        => await Context.OutletLegalPolicyVersions.AsNoTracking()
            .Where(x => x.OutletId == outletId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

    public async Task AddVersionAsync(OutletLegalPolicyVersion version)
    {
        Context.OutletLegalPolicyVersions.Add(version);
        await SaveAsync();
    }

    public async Task UnpublishOthersAsync(Guid outletId, string publishedVersion)
    {
        var rows = await Context.OutletLegalPolicyVersions
            .Where(x => x.OutletId == outletId && x.IsPublished && x.Version != publishedVersion)
            .ToListAsync();
        foreach (var row in rows)
            row.IsPublished = false;
        if (rows.Count > 0)
            await SaveAsync();
    }

    public Task<bool> HasAcceptedVersionAsync(Guid customerId, Guid outletId, Guid versionId)
        => Context.CustomerLegalAcceptances.AnyAsync(x =>
            x.CustomerId == customerId &&
            x.OutletId == outletId &&
            x.LegalPolicyVersionId == versionId &&
            x.TermsAccepted &&
            x.PrivacyAccepted);

    public async Task AddAcceptanceAsync(CustomerLegalAcceptance acceptance)
    {
        Context.CustomerLegalAcceptances.Add(acceptance);
        await SaveAsync();
    }
}