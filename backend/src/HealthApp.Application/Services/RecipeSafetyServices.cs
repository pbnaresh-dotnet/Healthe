using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class CatalogService(IIngredientRepository ingredients,IAllergenRepository allergens) : ICatalogService
{
    public async Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync() =>
        (await ingredients.GetActiveAsync()).Select(x=>new IngredientDto(x.Id,x.Name,x.DefaultUnit)).ToList();

    public async Task<IReadOnlyList<AllergenDto>> GetAllergensAsync() =>
        (await allergens.GetActiveAsync()).Select(x=>new AllergenDto(x.Id,x.Name)).ToList();
}

public sealed class AllergySafetyService(ICustomerProfileRepository profiles) : IAllergySafetyService
{
    public async Task<IReadOnlyList<AllergyWarningDto>> GetWarningsAsync(Guid customerId,IReadOnlyCollection<Recipe> recipes)
    {
        var profile=await profiles.GetAsync(customerId);
        var customerAllergens=profile?.Allergies.Select(x=>x.Allergen).Where(x=>x is not null).ToDictionary(x=>x.Id)??new Dictionary<Guid,Allergen>();
        var warnings=new List<AllergyWarningDto>();
        foreach(var recipe in recipes)
        {
            var matched=new Dictionary<Guid,string>();
            foreach(var ra in recipe.RecipeAllergens)
                if(customerAllergens.TryGetValue(ra.AllergenId,out var allergen)) matched[allergen.Id]=allergen.Name;
            foreach(var ri in recipe.RecipeIngredients)
                foreach(var ia in ri.Ingredient.Allergens)
                    if(customerAllergens.TryGetValue(ia.AllergenId,out var allergen)) matched[allergen.Id]=allergen.Name;
            if(matched.Count>0)
            {
                warnings.Add(new AllergyWarningDto(
                    recipe.Id,
                    recipe.Name,
                    matched.Values.OrderBy(x=>x).ToList(),
                    $"This meal may contain {string.Join(", ",matched.Values.OrderBy(x=>x))} that matches an allergy in your profile. Please review the ingredients before continuing."));
            }
        }
        return warnings;
    }

    public async Task EnsureConfirmedAsync(Guid customerId,IReadOnlyCollection<Recipe> recipes,IReadOnlyCollection<Guid>? confirmedRecipeIds)
    {
        var warnings=await GetWarningsAsync(customerId,recipes);
        var confirmed=confirmedRecipeIds??[];
        var missing=warnings.Where(x=>!confirmed.Contains(x.RecipeId)).Select(x=>x.RecipeName).ToList();
        if(missing.Count>0) throw new InvalidOperationException($"Allergy confirmation is required for: {string.Join(", ",missing)}");
    }
}
