using HealthApp.Application.Abstractions;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class IngredientConsumptionService(
    ICurrentUser current,
    IOutletRepository outlets,
    IDeliveryRepository deliveries,
    ISubscriptionRepository subscriptions,
    ISubscriptionMealSelectionRepository selections,
    IRecipeRepository recipes) : IIngredientConsumptionService
{
    private static readonly HashSet<MealSelectionStatus> NonConsumableStatuses =
    [
        MealSelectionStatus.Skipped,
        MealSelectionStatus.Unused,
        MealSelectionStatus.Rescheduled,
        MealSelectionStatus.Expired,
        MealSelectionStatus.Cancelled
    ];

    public async Task<DailyIngredientConsumptionReportDto> GetDailyAsync(DateTime date)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("The current user is not associated with an outlet.");

        var outlet = await outlets.GetByIdAsync(outletId)
            ?? throw new KeyNotFoundException("Outlet not found.");

        var day = date.Date;
        var nextDay = day.AddDays(1);

        var deliveredDeliveries = (await deliveries.GetByOutletAsync(outletId))
            .Where(x => x.ScheduledDate.Date == day && x.Status == DeliveryStatus.Delivered)
            .OrderBy(x => x.MealSlot)
            .ThenBy(x => x.ScheduledDate)
            .ToList();

        if (deliveredDeliveries.Count == 0)
            return new(day, outlet.Name, 0, 0, []);

        var outletSubscriptions = (await subscriptions.GetByOutletAsync(outletId))
            .ToDictionary(x => x.Id);

        var selectedMeals = await selections.GetByOutletAndDateRangeAsync(outletId, day, nextDay);
        var usedSelectionIds = new HashSet<Guid>();
        var consumableSelections = new List<(SubscriptionMealSelection Selection, Delivery Delivery)>();

        foreach (var delivery in deliveredDeliveries)
        {
            if (!outletSubscriptions.TryGetValue(delivery.SubscriptionId, out var subscription))
                continue;

            var candidates = subscription.DeliveryMode == SubscriptionDeliveryMode.OneDeliveryPerDay
                ? selectedMeals.Where(x =>
                    x.SubscriptionId == delivery.SubscriptionId &&
                    x.MealDate.Date == day &&
                    !NonConsumableStatuses.Contains(x.Status))
                : selectedMeals.Where(x =>
                    x.SubscriptionId == delivery.SubscriptionId &&
                    x.MealDate.Date == day &&
                    x.MealSlot == delivery.MealSlot &&
                    !NonConsumableStatuses.Contains(x.Status));

            foreach (var selection in candidates)
            {
                // A one-delivery-per-day delivery can cover several meal selections.
                // De-duplicate them so a delivery record cannot double-count ingredient usage.
                if (usedSelectionIds.Add(selection.Id))
                    consumableSelections.Add((selection, delivery));
            }
        }

        if (consumableSelections.Count == 0)
            return new(day, outlet.Name, deliveredDeliveries.Count, 0, []);

        var recipeIds = consumableSelections.Select(x => x.Selection.RecipeId).Distinct().ToList();
        var recipeRows = await recipes.GetByIdsForOutletAsync(recipeIds, outletId);
        var recipeById = recipeRows.ToDictionary(x => x.Id);

        var usage = new Dictionary<Guid, IngredientUsageAccumulator>();

        foreach (var (selection, delivery) in consumableSelections)
        {
            if (!recipeById.TryGetValue(selection.RecipeId, out var recipe))
                continue;

            foreach (var recipeIngredient in recipe.RecipeIngredients)
            {
                var ingredient = recipeIngredient.Ingredient;
                var (quantity, unit) = NormalizeQuantity(
                    recipeIngredient.Quantity,
                    recipeIngredient.Unit,
                    ingredient.DefaultUnit);

                if (quantity <= 0)
                    continue;

                if (!usage.TryGetValue(ingredient.Id, out var row))
                {
                    row = new IngredientUsageAccumulator(ingredient.Id, ingredient.Name, unit);
                    usage.Add(ingredient.Id, row);
                }

                if (!string.Equals(row.Unit, unit, StringComparison.OrdinalIgnoreCase))
                {
                    // Current catalog uses gram-based recipe quantities. Preserve a
                    // clear unit conflict instead of silently mixing incomparable units.
                    throw new InvalidOperationException(
                        $"Ingredient '{ingredient.Name}' is configured with mixed usage units ({row.Unit} and {unit}). Normalize the recipe ingredient units before using the consumption report.");
                }

                row.TotalQuantity += quantity;
                row.MealCount++;
                row.DeliveryIds.Add(delivery.Id);
                row.RecipeNames.Add(recipe.Name);
            }
        }

        var rows = usage.Values
            .OrderBy(x => x.Name)
            .Select(x => new IngredientConsumptionRowDto(
                x.Id,
                x.Name,
                decimal.Round(x.TotalQuantity, 3, MidpointRounding.AwayFromZero),
                x.Unit,
                x.MealCount,
                x.DeliveryIds.Count,
                x.RecipeNames.OrderBy(n => n).ToList()))
            .ToList();

        return new(day, outlet.Name, deliveredDeliveries.Count, consumableSelections.Count, rows);
    }

    private static (decimal Quantity, string Unit) NormalizeQuantity(
        decimal quantity,
        string? unitValue,
        string? defaultUnitValue)
    {
        var unit = (unitValue ?? "").Trim().ToLowerInvariant();
        var defaultUnit = (defaultUnitValue ?? "g").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(unit))
            unit = defaultUnit;

        if (unit == defaultUnit)
            return (quantity, defaultUnit);

        if (unit is "kg" or "kilogram" or "kilograms")
        {
            if (defaultUnit is "g" or "gram" or "grams")
                return (quantity * 1000m, "g");
            if (defaultUnit is "kg" or "kilogram" or "kilograms")
                return (quantity, "kg");
        }

        if (unit is "g" or "gram" or "grams")
        {
            if (defaultUnit is "kg" or "kilogram" or "kilograms")
                return (quantity / 1000m, "kg");
            if (defaultUnit is "g" or "gram" or "grams")
                return (quantity, "g");
        }

        return (quantity, unit);
    }

    private sealed class IngredientUsageAccumulator(Guid id, string name, string unit)
    {
        public Guid Id { get; } = id;
        public string Name { get; } = name;
        public string Unit { get; } = unit;
        public decimal TotalQuantity { get; set; }
        public int MealCount { get; set; }
        public HashSet<Guid> DeliveryIds { get; } = [];
        public HashSet<string> RecipeNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
