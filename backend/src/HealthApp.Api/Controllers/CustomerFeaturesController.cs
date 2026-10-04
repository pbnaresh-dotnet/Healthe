using HealthApp.Domain.Entities;
using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/customer"), Authorize(Roles="Customer")]
public sealed class CustomerFeaturesController(ICustomerProfileService profile, ICustomerAddressService addresses, IPaymentService payments, ICustomerLikedMealRepository likedMeals, IRecipeRepository recipes) : ControllerBase
{
    [HttpGet("profile/preferences")] public async Task<IActionResult> GetProfile() => Ok(await profile.GetAsync());
    [HttpPut("profile/preferences")] public async Task<IActionResult> SaveProfile(SaveCustomerProfileRequest request) => Ok(await profile.SaveAsync(request));
    [HttpGet("liked-meals")] public async Task<IActionResult> LikedMeals()
    {
        var liked = await likedMeals.GetByCustomerAsync(GetCustomerId());
        var recipeIds = liked.Select(x => x.RecipeId).ToHashSet();
        if (recipeIds.Count == 0) return Ok(Array.Empty<CustomerLikedMealDto>());

        var result = (await recipes.GetByIdsAsync(recipeIds))
            .Select(x => new CustomerLikedMealDto(x.Id, x.Name, x.ImageUrl, x.Calories, x.ProteinGrams, x.CarbsGrams, x.FatGrams, x.Category.ToString(), x.PricePerMeal))
            .ToList();
        return Ok(result);
    }

    [HttpPost("liked-meals/{recipeId:guid}")]
    public async Task<IActionResult> LikeMeal(Guid recipeId)
    {
        var recipe = await recipes.GetAsync(recipeId);
        if (recipe is null || !recipe.IsActive) return NotFound();
        var customerId = GetCustomerId();
        if (!await likedMeals.ExistsAsync(customerId, recipeId))
        {
            await likedMeals.AddAsync(new CustomerLikedMeal
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                RecipeId = recipeId
            });
        }
        return Ok(new CustomerLikedMealDto(recipe.Id, recipe.Name, recipe.ImageUrl, recipe.Calories, recipe.ProteinGrams, recipe.CarbsGrams, recipe.FatGrams, recipe.Category.ToString(), recipe.PricePerMeal));
    }

    [HttpDelete("liked-meals/{recipeId:guid}")]
    public async Task<IActionResult> UnlikeMeal(Guid recipeId)
    {
        await likedMeals.RemoveAsync(GetCustomerId(), recipeId);
        return NoContent();
    }

    [HttpGet("addresses")] public async Task<IActionResult> Addresses() => Ok(await addresses.GetAsync());
    [HttpPost("addresses")] public async Task<IActionResult> CreateAddress(CreateCustomerAddressRequest request) => Ok(await addresses.CreateAsync(request));
    [HttpPut("addresses/{id:guid}")] public async Task<IActionResult> UpdateAddress(Guid id, UpdateCustomerAddressRequest request) { var x=await addresses.UpdateAsync(id,request); return x is null?NotFound():Ok(x); }
    [HttpDelete("addresses/{id:guid}")] public async Task<IActionResult> DeleteAddress(Guid id)=>await addresses.DeleteAsync(id)?NoContent():NotFound();
    [HttpGet("outlets/{outletId:guid}/delivery-quotes")] public async Task<IActionResult> DeliveryQuotes(Guid outletId)=>Ok(await addresses.QuoteAsync(outletId));
    [HttpPost("payments")] public async Task<IActionResult> CreatePayment(CreatePaymentRequest request)=>Ok(await payments.CreateAsync(request));
    [HttpGet("payments/{id:guid}")] public async Task<IActionResult> GetPayment(Guid id){var x=await payments.GetAsync(id);return x is null?NotFound():Ok(x);}
    private Guid GetCustomerId() => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
}
