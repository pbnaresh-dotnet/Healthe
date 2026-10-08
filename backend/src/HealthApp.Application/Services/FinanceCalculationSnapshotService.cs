using HealthApp.Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthApp.Application.Abstractions;
using HealthApp.Application.Strategies;

namespace HealthApp.Application.Services;

public sealed class FinanceCalculationSnapshotService(
    IFinanceCalculationSnapshotRepository snapshots,
    IFinancePolicyService policy) : IFinanceCalculationSnapshotService
{
    public async Task<FinanceCalculationSnapshot> CreateAsync(
        Guid outletId,
        string sourceType,
        Guid sourceId,
        DateTime calculatedAtUtc,
        FinanceTaxCalculationConfiguration configuration,
        TaxBreakdown calculation,
        decimal restaurantBaseAmount,
        decimal platformServiceFee,
        decimal platformServiceFeePercent,
        bool gatewayCostsIncludedInPlatformFee,
        decimal commissionRatePercent,
        decimal commissionAmount,
        Guid? discountTierId = null,
        decimal discountPercent = 0m,
        decimal discountAmount = 0m,
        string discountRuleSnapshotJson = "",
        Guid? discountCodeId = null,
        decimal discountCodeAmount = 0m,
        decimal totalDiscountAmount = 0m)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Finance snapshot source type is required.", nameof(sourceType));
        if (sourceId == Guid.Empty)
            throw new ArgumentException("Finance snapshot source id is required.", nameof(sourceId));
        if (commissionRatePercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(commissionRatePercent), commissionRatePercent, "Commission rate must be between 0% and 100%.");
        if (discountPercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(discountPercent), discountPercent, "Discount rate must be between 0% and 100%.");
        if (discountCodeAmount < 0m || totalDiscountAmount < 0m)
            throw new ArgumentOutOfRangeException(nameof(discountCodeAmount), "Discount amounts cannot be negative.");
        if (discountAmount < 0m)
            throw new ArgumentOutOfRangeException(nameof(discountAmount), discountAmount, "Discount amount cannot be negative.");
        if (platformServiceFeePercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(platformServiceFeePercent), platformServiceFeePercent, "Platform service fee rate must be between 0% and 100%.");

        var existing = await snapshots.GetBySourceAsync(sourceType.Trim(), sourceId);
        if (existing is not null)
            return existing;

        var policyDocument = await policy.GetAsync(asOfUtc: calculatedAtUtc);
        var policyVersionId = policyDocument?.CurrentVersion?.Id;

        var inputs = new
        {
            outletId,
            sourceType = sourceType.Trim(),
            sourceId,
            calculatedAtUtc,
            restaurantBaseAmount,
            platformServiceFee,
            platformServiceFeePercent,
            gatewayCostsIncludedInPlatformFee,
            commissionRatePercent,
            discountTierId,
            discountPercent,
            discountAmount,
            discountRuleSnapshotJson,
            discountCodeId,
            discountCodeAmount,
            totalDiscountAmount,
            restaurantProfileId = configuration.Restaurant.ProfileId,
            commissionTaxRuleId = configuration.PlatformCommission.Id,
            commissionTaxRuleCode = configuration.PlatformCommission.Code,
            restaurantTaxRuleId = configuration.Restaurant.Rule.Id,
            restaurantTaxRuleCode = configuration.Restaurant.Rule.Code,
            platformTaxProfileId = configuration.PlatformTaxProfileId,
            platformTaxApplicable = configuration.PlatformTaxApplicable,
            platformTaxRuleId = configuration.PlatformService.Id,
            platformTaxRuleCode = configuration.PlatformService.Code,
            restaurantTaxApplicable = configuration.Restaurant.IsApplicable,
            restaurantTaxOperatingMode = configuration.Restaurant.TaxOperatingMode.ToString(),
            restaurantGstMode = configuration.Restaurant.PricingMode.ToString()
        };

        var results = new
        {
            restaurantRate = calculation.RestaurantRate,
            restaurantTaxableAmount = calculation.RestaurantTaxableAmount,
            restaurantTaxAmount = calculation.RestaurantAmount,
            restaurantTaxApplicable = calculation.RestaurantTaxApplicable,
            restaurantCgstRate = calculation.RestaurantCgstRate,
            restaurantSgstRate = calculation.RestaurantSgstRate,
            restaurantIgstRate = calculation.RestaurantIgstRate,
            platformServiceFee,
            platformServiceFeePercent,
            gatewayCostsIncludedInPlatformFee,
            platformTaxRate = calculation.PlatformRate,
            platformTaxAmount = calculation.PlatformAmount,
            platformCgstRate = calculation.PlatformCgstRate,
            platformSgstRate = calculation.PlatformSgstRate,
            platformIgstRate = calculation.PlatformIgstRate,
            commissionBaseAmount = calculation.RestaurantTaxableAmount,
            commissionRatePercent,
            commissionAmount,
            discountTierId,
            discountPercent,
            discountAmount,
            discountCodeId,
            discountCodeAmount,
            totalDiscountAmount,
            commissionTaxRuleCode = configuration.PlatformCommission.Code,
            commissionTaxRate = configuration.PlatformCommission.TaxRatePercent,
            commissionTaxAmount = configuration.PlatformTaxApplicable ? Math.Round(commissionAmount * configuration.PlatformCommission.TaxRatePercent / 100m, 2) : 0m
        };

        var inputJson = JsonSerializer.Serialize(inputs);
        var resultJson = JsonSerializer.Serialize(results);
        var inputHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(inputJson)))
            .ToLowerInvariant();

        var snapshot = new FinanceCalculationSnapshot
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            SourceType = sourceType.Trim(),
            SourceId = sourceId,
            CalculatedAtUtc = calculatedAtUtc,
            FinancePolicyDocumentVersionId = policyVersionId,
            PlatformTaxProfileId = configuration.PlatformTaxProfileId,
            TaxProfileId = configuration.Restaurant.ProfileId,
            RestaurantTaxRuleId = configuration.Restaurant.Rule.Id,
            PlatformTaxRuleId = configuration.PlatformService.Id,
            CommissionTaxRuleId = configuration.PlatformCommission.Id,
            PlatformTaxApplicable = configuration.PlatformTaxApplicable,
            RestaurantTaxApplicable = calculation.RestaurantTaxApplicable,
            RestaurantTaxOperatingMode = configuration.Restaurant.TaxOperatingMode,
            RestaurantGstMode = configuration.Restaurant.PricingMode,
            RestaurantRate = calculation.RestaurantRate,
            RestaurantTaxableAmount = calculation.RestaurantTaxableAmount,
            RestaurantTaxAmount = calculation.RestaurantAmount,
            PlatformServiceFee = platformServiceFee,
            PlatformTaxRate = calculation.PlatformRate,
            PlatformTaxAmount = calculation.PlatformAmount,
            CommissionTaxRate = configuration.PlatformTaxApplicable ? configuration.PlatformCommission.TaxRatePercent : 0m,
            CommissionTaxAmount = configuration.PlatformTaxApplicable ? Math.Round(commissionAmount * configuration.PlatformCommission.TaxRatePercent / 100m, 2) : 0m,
            CommissionBaseAmount = calculation.RestaurantTaxableAmount,
            CommissionRatePercent = commissionRatePercent,
            CommissionAmount = commissionAmount,
            DiscountTierId = discountTierId,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            DiscountRuleSnapshotJson = discountRuleSnapshotJson ?? "",
            DiscountCodeId = discountCodeId,
            DiscountCodeAmount = discountCodeAmount,
            TotalDiscountAmount = totalDiscountAmount,
            InputHash = inputHash,
            InputsJson = inputJson,
            ResultsJson = resultJson,
            CreatedAtUtc = DateTime.UtcNow
        };

        await snapshots.AddAsync(snapshot);
        return snapshot;
    }
}
