using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/marketplace")]
public sealed class MarketplaceController(IMarketplaceService service, ICityAreaRepository areas) : ControllerBase
{
    [HttpGet("saas-plans")] public async Task<IActionResult> SaaSPlans() => Ok(await service.GetSaaSPlansAsync());
    [HttpGet("availability")] public async Task<IActionResult> Availability([FromQuery] double latitude,[FromQuery] double longitude)=>Ok(await service.GetAvailabilityAsync(latitude,longitude));
    [HttpGet("outlets")] public async Task<IActionResult> Outlets()=>Ok(await service.GetAllOutletsAsync());
    [HttpGet("outlets/{slug}")] public async Task<IActionResult> Outlet(string slug){var x=await service.GetOutletAsync(slug);return x is null?NotFound():Ok(x);}
    [HttpGet("outlets/{outletId:guid}/meal-plans")] public async Task<IActionResult> Plans(Guid outletId)=>Ok(await service.GetPlansAsync(outletId));
    [HttpGet("outlets/{outletId:guid}/recipes")] public async Task<IActionResult> Recipes(Guid outletId,[FromQuery]string? category)=>Ok(await service.GetRecipesAsync(outletId,category));
    [HttpGet("outlets/{outletId:guid}/menu")] public async Task<IActionResult> Menu(Guid outletId)=>Ok(await service.GetMenuAsync(outletId));
    [HttpGet("city-areas")] public async Task<IActionResult> CityAreas([FromQuery]string? city)=>Ok((await areas.GetActiveAsync(city)).Select(x=>new CityAreaDto(x.Id,x.City,x.State,x.Name,x.Pincode,x.Latitude,x.Longitude,x.IsActive)));
}
