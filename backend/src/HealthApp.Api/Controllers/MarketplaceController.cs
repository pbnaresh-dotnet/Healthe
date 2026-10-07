using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/marketplace")]
public sealed class MarketplaceController(IMarketplaceService service, ICityAreaRepository areas, IGeocodingService geocoding) : ControllerBase
{
    [HttpGet("saas-plans")] public async Task<IActionResult> SaaSPlans() => Ok(await service.GetSaaSPlansAsync());
    [HttpGet("availability")] public async Task<IActionResult> Availability([FromQuery] double latitude,[FromQuery] double longitude,[FromQuery]string? city)=>Ok(await service.GetAvailabilityAsync(latitude,longitude,city));
    [HttpGet("cities")] public async Task<IActionResult> Cities()=>Ok(await service.GetCitiesAsync());
    [HttpGet("outlets")] public async Task<IActionResult> Outlets([FromQuery]string? city)=>Ok(await service.GetAllOutletsAsync(city));
    [HttpGet("outlets/{slug}")] public async Task<IActionResult> Outlet(string slug){var x=await service.GetOutletAsync(slug);return x is null?NotFound():Ok(x);}
    [HttpGet("outlets/{slug}/legal")] public async Task<IActionResult> Legal(string slug){var x=await service.GetOutletLegalAsync(slug);return x is null?NotFound():Ok(x);}
    [HttpGet("outlets/{outletId:guid}/meal-plans")] public async Task<IActionResult> Plans(Guid outletId)=>Ok(await service.GetPlansAsync(outletId));
    [HttpGet("outlets/{outletId:guid}/recipes")] public async Task<IActionResult> Recipes(Guid outletId,[FromQuery]string? category)=>Ok(await service.GetRecipesAsync(outletId,category));
    [HttpGet("outlets/{outletId:guid}/menu")] public async Task<IActionResult> Menu(Guid outletId)=>Ok(await service.GetMenuAsync(outletId));
    [HttpGet("city-areas")] public async Task<IActionResult> CityAreas([FromQuery]string? city)=>Ok((await areas.GetActiveAsync(city)).Select(x=>new CityAreaDto(x.Id,x.City,x.State,x.Name,x.Pincode,x.Latitude,x.Longitude,x.IsActive)));
    [HttpGet("reverse-geocode")] public async Task<IActionResult> ReverseGeocode([FromQuery]double latitude,[FromQuery]double longitude){if(double.IsNaN(latitude)||double.IsInfinity(latitude)||latitude is < -90 or > 90)return BadRequest(new {message="Latitude must be between -90 and 90."});if(double.IsNaN(longitude)||double.IsInfinity(longitude)||longitude is < -180 or > 180)return BadRequest(new {message="Longitude must be between -180 and 180."});var result=await geocoding.ReverseAsync(latitude,longitude);return result is null?NotFound(new {message="No address could be resolved for this location."}):Ok(result);}
}
