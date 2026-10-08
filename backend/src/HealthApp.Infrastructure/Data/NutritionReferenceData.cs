using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Data;

/// <summary>
/// Maintained seed/reference values for the initial ingredient catalog.
/// Values are per 100 g of the exact food state described by the ingredient name.
/// The database remains the runtime source of truth so these rows can be replaced
/// later by an admin-managed nutrition master without changing recipe logic.
/// </summary>
internal static class NutritionReferenceData
{
    private sealed record Reference(
        decimal Calories,
        decimal Protein,
        decimal Carbs,
        decimal Fat,
        decimal Fiber,
        decimal Sugar,
        string Source,
        string ReferenceId);

    private static readonly IReadOnlyDictionary<string, Reference> Values =
        new Dictionary<string, Reference>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chicken Breast"] = new(151m, 30.5m, 0m, 3.2m, 0m, 0m, "USDA FoodData Central · cooked grilled, skinless boneless", "FDC 171534"),
            ["Brown Rice"] = new(123m, 2.74m, 25.58m, 0.97m, 1.6m, 0.0m, "USDA FoodData Central · cooked long-grain brown rice", "FDC 169704"),
            ["Broccoli"] = new(35m, 2.38m, 7.18m, 0.41m, 3.3m, 1.39m, "USDA FoodData Central · cooked boiled drained without salt", "FDC 169967"),
            ["Chickpeas"] = new(164m, 8.86m, 27.42m, 2.59m, 7.6m, 4.8m, "USDA FoodData Central · cooked boiled chickpeas", "FDC 173757"),
            ["Lentils"] = new(116m, 9.02m, 20.13m, 0.38m, 7.9m, 1.8m, "USDA FoodData Central · cooked boiled lentils", "FDC 172421"),
            ["Olive Oil"] = new(884m, 0m, 0m, 100m, 0m, 0m, "USDA FoodData Central · olive oil", "FDC 171413"),
            ["Tahini"] = new(595m, 17m, 21.19m, 53.76m, 9.3m, 0.5m, "USDA FoodData Central · tahini from roasted sesame kernels", "FDC 170189"),
            ["Prawns"] = new(99m, 23.98m, 0.2m, 0.28m, 0m, 0m, "USDA FoodData Central · cooked shrimp", "FDC 175180"),
        };

    public static async Task ApplyAsync(HealthAppDbContext db, CancellationToken cancellationToken)
    {
        var ingredients = await db.Ingredients.ToListAsync(cancellationToken);

        foreach (var ingredient in ingredients)
        {
            if (!Values.TryGetValue(ingredient.Name, out var reference))
                continue;

            ingredient.CaloriesPer100g = reference.Calories;
            ingredient.ProteinGramsPer100g = reference.Protein;
            ingredient.CarbsGramsPer100g = reference.Carbs;
            ingredient.FatGramsPer100g = reference.Fat;
            ingredient.FiberGramsPer100g = reference.Fiber;
            ingredient.SugarGramsPer100g = reference.Sugar;
            ingredient.NutritionSource = reference.Source;
            ingredient.NutritionReferenceId = reference.ReferenceId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
