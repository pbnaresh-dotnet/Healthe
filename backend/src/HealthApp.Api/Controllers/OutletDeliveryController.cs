using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/outlets/me"), Authorize(Roles="OutletAdmin")]
public sealed class OutletDeliveryController(IOutletDeliveryService delivery, IDiscountConfigurationService discounts) : ControllerBase
{
    [HttpGet("delivery-areas/available")] public async Task<IActionResult> AvailableAreas([FromQuery]string? city)=>Ok(await delivery.GetAvailableAreasAsync(city));
    [HttpGet("delivery-areas")] public async Task<IActionResult> Areas()=>Ok(await delivery.GetAreasAsync());
    [HttpPut("delivery-areas")] public async Task<IActionResult> SaveAreas(SaveOutletDeliveryAreasRequest request)=>Ok(await delivery.SaveAreasAsync(request));
    [HttpGet("delivery-pricing")] public async Task<IActionResult> Pricing()=>Ok(await delivery.GetPricingAsync());
    [HttpPost("delivery-pricing")] public async Task<IActionResult> AddPricing(CreateDeliveryPricingRuleRequest request)=>Ok(await delivery.AddPricingAsync(request));
    [HttpDelete("delivery-pricing/{id:guid}")] public async Task<IActionResult> DeletePricing(Guid id)=>await delivery.DeletePricingAsync(id)?NoContent():NotFound();
    [HttpGet("discount-tiers")] public async Task<IActionResult> DiscountTiers()=>Ok(await discounts.GetTiersAsync());
    [HttpPost("discount-tiers")] public async Task<IActionResult> AddDiscountTier(SaveSubscriptionDiscountTierRequest request)=>Ok(await discounts.AddTierAsync(request));
    [HttpPut("discount-tiers/{id:guid}")] public async Task<IActionResult> UpdateDiscountTier(Guid id,SaveSubscriptionDiscountTierRequest request){var x=await discounts.UpdateTierAsync(id,request);return x is null?NotFound():Ok(x);}
    [HttpDelete("discount-tiers/{id:guid}")] public async Task<IActionResult> DeleteDiscountTier(Guid id)=>await discounts.DeleteTierAsync(id)?NoContent():NotFound();
}
