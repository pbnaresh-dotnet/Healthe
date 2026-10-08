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
        decimal commissionRatePercent,
        decimal commissionAmount)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Finance snapshot source type is required.", nameof(sourceType));
        if (sourceId == Guid.Empty)
            throw new ArgumentException("Finance snapshot source id is required.", nameof(sourceId));

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
            commissionRatePercent,
            restaurantProfileId = configuration.Restaurant.ProfileId,
            restaurantTaxRuleId = configuration.Restaurant.Rule.Id,
            restaurantTaxRuleCode = configuration.Restaurant.Rule.Code,
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
            platformTaxRate = calculation.PlatformRate,
            platformTaxAmount = calculation.PlatformAmount,
            platformCgstRate = calculation.PlatformCgstRate,
            platformSgstRate = calculation.PlatformSgstRate,
            platformIgstRate = calculation.PlatformIgstRate,
            commissionBaseAmount = calculation.RestaurantTaxableAmount,
            commissionRatePercent,
            commissionAmount
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
            TaxProfileId = configuration.Restaurant.ProfileId,
            RestaurantTaxRuleId = configuration.Restaurant.Rule.Id,
            PlatformTaxRuleId = configuration.PlatformService.Id,
            RestaurantTaxApplicable = calculation.RestaurantTaxApplicable,
            RestaurantTaxOperatingMode = configuration.Restaurant.TaxOperatingMode,
            RestaurantGstMode = configuration.Restaurant.PricingMode,
            RestaurantRate = calculation.RestaurantRate,
            RestaurantTaxableAmount = calculation.RestaurantTaxableAmount,
            RestaurantTaxAmount = calculation.RestaurantAmount,
            PlatformServiceFee = platformServiceFee,
            PlatformTaxRate = calculation.PlatformRate,
            PlatformTaxAmount = calculation.PlatformAmount,
            CommissionBaseAmount = calculation.RestaurantTaxableAmount,
            CommissionRatePercent = commissionRatePercent,
            CommissionAmount = commissionAmount,
            InputHash = inputHash,
            InputsJson = inputJson,
            ResultsJson = resultJson,
            CreatedAtUtc = DateTime.UtcNow
        };

        await snapshots.AddAsync(snapshot);
        return snapshot;
    }
}
