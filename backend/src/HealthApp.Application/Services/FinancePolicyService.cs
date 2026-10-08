using System.Security.Cryptography;
using System.Text;
using HealthApp.Application.Abstractions;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class FinancePolicyService(IFinancePolicyRepository repository) : IFinancePolicyService
{
    public async Task<FinancePolicyDocumentDto?> GetAsync(string code = "FINANCE-CALCULATION-POLICY", DateTime? asOfUtc = null)
    {
        var document = await repository.GetDocumentAsync(code.Trim());
        if (document is null) return null;

        var versions = await repository.GetVersionsAsync(document.Id);
        var at = asOfUtc ?? DateTime.UtcNow;

        var current = versions
            .Where(x => x.Status == FinancePolicyPublicationStatus.Published &&
                        x.EffectiveFromUtc <= at &&
                        (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > at))
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        return new FinancePolicyDocumentDto(
            document.Id,
            document.Code,
            document.Title,
            document.Description,
            current is null ? null : await MapVersionAsync(current),
            await MapVersionsAsync(versions));
    }

    public async Task<IReadOnlyList<FinancePolicyVersionDto>> GetHistoryAsync(string code = "FINANCE-CALCULATION-POLICY")
    {
        var document = await repository.GetDocumentAsync(code.Trim());
        if (document is null) return [];
        return await MapVersionsAsync(await repository.GetVersionsAsync(document.Id));
    }

    private async Task<IReadOnlyList<FinancePolicyVersionDto>> MapVersionsAsync(IReadOnlyList<FinancePolicyDocumentVersion> versions)
    {
        var result = new List<FinancePolicyVersionDto>(versions.Count);
        foreach (var version in versions)
            result.Add(await MapVersionAsync(version));
        return result;
    }

    private async Task<FinancePolicyVersionDto> MapVersionAsync(FinancePolicyDocumentVersion version)
    {
        var sections = await repository.GetSectionsAsync(version.Id);
        return new FinancePolicyVersionDto(
            version.Id,
            version.Version,
            version.Status.ToString(),
            version.EffectiveFromUtc,
            version.EffectiveToUtc,
            version.ChangeSummary,
            version.ChangeReason,
            version.SourceCodeReference,
            version.ContentHash,
            version.PreviousVersionId,
            version.CreatedAtUtc,
            version.ReviewedAtUtc,
            version.PublishedAtUtc,
            sections.Select(x => new FinancePolicySectionDto(
                x.Id, x.SectionCode, x.Title, x.DisplayOrder, x.ContentMarkdown)).ToList());
    }

    public static string ComputeContentHash(IEnumerable<FinancePolicySectionUpdateRequest> sections)
    {
        var canonical = string.Join("\n---\n",
            sections.OrderBy(x => x.DisplayOrder).ThenBy(x => x.SectionCode)
                .Select(x => $"{x.SectionCode.Trim()}\n{x.Title.Trim()}\n{x.ContentMarkdown.Trim()}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
