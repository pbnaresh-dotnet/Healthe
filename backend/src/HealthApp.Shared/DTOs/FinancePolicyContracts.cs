namespace HealthApp.Shared.DTOs;

public record FinancePolicySectionDto(
    Guid Id,
    string SectionCode,
    string Title,
    int DisplayOrder,
    string ContentMarkdown);

public record FinancePolicyVersionDto(
    Guid Id,
    string Version,
    string Status,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string ChangeSummary,
    string ChangeReason,
    string SourceCodeReference,
    string ContentHash,
    Guid? PreviousVersionId,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? PublishedAtUtc,
    IReadOnlyList<FinancePolicySectionDto> Sections);

public record FinancePolicyDocumentDto(
    Guid Id,
    string Code,
    string Title,
    string Description,
    FinancePolicyVersionDto? CurrentVersion,
    IReadOnlyList<FinancePolicyVersionDto> History);

public record FinancePolicySectionUpdateRequest(
    string SectionCode,
    string Title,
    int DisplayOrder,
    string ContentMarkdown);

public record FinancePolicyDraftRequest(
    string Version,
    DateTime EffectiveFromUtc,
    string ChangeSummary,
    string ChangeReason,
    string SourceCodeReference,
    IReadOnlyList<FinancePolicySectionUpdateRequest> Sections);

public record OutletTaxSettingsDto(
    decimal RestaurantGstRate,
    string RestaurantGstMode,
    bool IsGstRegistered,
    bool IsComposition,
    string TaxOperatingMode,
    string Gstin,
    string Pan,
    DateTime EffectiveFromUtc);

public record UpdateOutletTaxSettingsRequest(
    decimal RestaurantGstRate,
    string RestaurantGstMode,
    bool IsGstRegistered = false,
    bool IsComposition = false,
    string TaxOperatingMode = "DirectOutletSupplier",
    string Gstin = "",
    string Pan = "",
    DateTime? EffectiveFromUtc = null);
