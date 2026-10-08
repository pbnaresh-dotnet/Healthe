using HealthApp.Domain.Enums;

namespace HealthApp.Application.Strategies;

public sealed record FinanceTaxRuleSnapshot(
    Guid Id,
    string Code,
    FinanceSupplyType SupplyType,
    TaxOperatingMode? TaxOperatingMode,
    decimal TaxRatePercent,
    decimal CgstRatePercent,
    decimal SgstRatePercent,
    decimal IgstRatePercent,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc);

public sealed record RestaurantTaxConfiguration(
    Guid ProfileId,
    bool IsApplicable,
    bool IsGstRegistered,
    bool IsComposition,
    TaxOperatingMode TaxOperatingMode,
    GstMode PricingMode,
    FinanceTaxRuleSnapshot Rule);

public sealed record FinanceTaxCalculationConfiguration(
    RestaurantTaxConfiguration Restaurant,
    FinanceTaxRuleSnapshot PlatformService);
