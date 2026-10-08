using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
namespace HealthApp.Application.Strategies;

public sealed record PackagePricingContext(SubscriptionDuration Duration, IReadOnlyCollection<SubscriptionMealSelection> Meals);

public sealed record PackageDiscount(decimal Percent, decimal Amount, Guid? TierId = null, int? TierMinMeals = null, int? TierMaxMeals = null);

public interface IPackageDiscountStrategy
{
    PackageDiscount Calculate(PackagePricingContext context, IReadOnlyList<SubscriptionDiscountTier>? tiers = null);
}
/// <summary>Centralized, replaceable discount policy. Tiers are intentionally configurable in one place.</summary>

public sealed class DurationAndVolumeDiscountStrategy : IPackageDiscountStrategy
{
    public PackageDiscount Calculate(PackagePricingContext context, IReadOnlyList<SubscriptionDiscountTier>? tiers = null)
    {
        var count = context.Meals.Count;
        var activeTiers = (tiers ?? []).Where(x => x.IsActive).OrderBy(x => x.MinMeals).ToList();
        if (activeTiers.Count == 0)
            return new(0m, 0m);

        var tier = activeTiers.FirstOrDefault(x => count >= x.MinMeals && (!x.MaxMeals.HasValue || count <= x.MaxMeals.Value));
        if (tier is null)
            return new(0m, 0m);

        var gross = Math.Round(context.Meals.Sum(x => x.MealPrice), 2);
        var configuredPercent = context.Duration switch {
            SubscriptionDuration.OneWeek => tier.OneWeekPercent,
            SubscriptionDuration.TwoWeeks => tier.TwoWeeksPercent,
            SubscriptionDuration.OneMonth => tier.OneMonthPercent,
            SubscriptionDuration.ThreeDays => 0m,
            SubscriptionDuration.FiveDays => 0m,
            _ => 0m
        };

        if (configuredPercent < 0m || configuredPercent > 100m)
            throw new InvalidOperationException("The selected outlet discount tier contains an invalid discount percentage.");

        return new(
            configuredPercent,
            Math.Round(gross * configuredPercent / 100m, 2),
            tier.Id,
            tier.MinMeals,
            tier.MaxMeals);
    }
}

public interface ILateSkipFeePolicy
{
    decimal GetFee(DateTime nowUtc, DateTime mealDateUtc, decimal configuredFee);
    bool IsLate(DateTime nowUtc, DateTime mealDateUtc);
}
/// <summary>Business rule: skipping before midnight on the delivery date is free; after midnight the outlet-configured fee applies.</summary>

public sealed class MidnightLateSkipFeePolicy : ILateSkipFeePolicy
{
    public bool IsLate(DateTime nowUtc, DateTime mealDateUtc) => ToIndiaTime(nowUtc).Date >= mealDateUtc.Date;
    public decimal GetFee(DateTime nowUtc, DateTime mealDateUtc, decimal configuredFee)
    {
        if (!IsLate(nowUtc, mealDateUtc)) return 0m;
        if (configuredFee < 0m || configuredFee > 100000m)
            throw new ArgumentOutOfRangeException(nameof(configuredFee), "Late-skip fee must be between ₹0 and ₹100,000.");
        return Math.Round(configuredFee, 2, MidpointRounding.AwayFromZero);
    }
    private static DateTime ToIndiaTime(DateTime utc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(GetIndiaZoneId());
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
    }
    private static string GetIndiaZoneId()
    {
        try {
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            return "Asia/Kolkata";
        }
        catch {
            return "India Standard Time";
        }
    }
}

public interface IMealPriceStrategy
{
    decimal GetPrice(Recipe recipe, MealPortionSize portion);
}

public sealed class RecipeMealPriceStrategy : IMealPriceStrategy
{
    public decimal GetPrice(Recipe recipe, MealPortionSize portion) =>
    portion == MealPortionSize.Large && recipe.LargePricePerMeal > 0
    ? recipe.LargePricePerMeal
    : recipe.PricePerMeal;
}

public interface IDeliveryModeStrategy
{
    SubscriptionDeliveryMode Mode {
        get;
    }
    bool CanShareDelivery(DateTime existingDate, DateTime mealDate, Guid existingAddressId, Guid mealAddressId);
}

public sealed class IndividualMealDeliveryStrategy : IDeliveryModeStrategy
{
    public SubscriptionDeliveryMode Mode => SubscriptionDeliveryMode.IndividualMealDelivery;
    public bool CanShareDelivery(DateTime existingDate, DateTime mealDate, Guid existingAddressId, Guid mealAddressId) => false;
}

public sealed class OneDeliveryPerDayStrategy : IDeliveryModeStrategy
{
    public SubscriptionDeliveryMode Mode => SubscriptionDeliveryMode.OneDeliveryPerDay;
    public bool CanShareDelivery(DateTime existingDate, DateTime mealDate, Guid existingAddressId, Guid mealAddressId) => existingDate.Date == mealDate.Date && existingAddressId == mealAddressId;
}

public interface IDeliveryModeStrategyFactory
{
    IDeliveryModeStrategy Create(SubscriptionDeliveryMode mode);
}

public sealed class DeliveryModeStrategyFactory(IEnumerable<IDeliveryModeStrategy> strategies) : IDeliveryModeStrategyFactory
{
    public IDeliveryModeStrategy Create(SubscriptionDeliveryMode mode) => strategies.FirstOrDefault(x => x.Mode == mode) ?? throw new InvalidOperationException($"No delivery strategy registered for {mode}.");
}
