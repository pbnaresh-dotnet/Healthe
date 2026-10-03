using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/customer"), Authorize(Roles="Customer")]
public sealed class CustomerFeaturesController(ICustomerProfileService profile, ICustomerAddressService addresses, IPaymentService payments) : ControllerBase
{
    [HttpGet("profile/preferences")] public async Task<IActionResult> GetProfile() => Ok(await profile.GetAsync());
    [HttpPut("profile/preferences")] public async Task<IActionResult> SaveProfile(SaveCustomerProfileRequest request) => Ok(await profile.SaveAsync(request));
    [HttpGet("addresses")] public async Task<IActionResult> Addresses() => Ok(await addresses.GetAsync());
    [HttpPost("addresses")] public async Task<IActionResult> CreateAddress(CreateCustomerAddressRequest request) => Ok(await addresses.CreateAsync(request));
    [HttpPut("addresses/{id:guid}")] public async Task<IActionResult> UpdateAddress(Guid id, UpdateCustomerAddressRequest request) { var x=await addresses.UpdateAsync(id,request); return x is null?NotFound():Ok(x); }
    [HttpDelete("addresses/{id:guid}")] public async Task<IActionResult> DeleteAddress(Guid id)=>await addresses.DeleteAsync(id)?NoContent():NotFound();
    [HttpGet("outlets/{outletId:guid}/delivery-quotes")] public async Task<IActionResult> DeliveryQuotes(Guid outletId)=>Ok(await addresses.QuoteAsync(outletId));
    [HttpPost("payments")] public async Task<IActionResult> CreatePayment(CreatePaymentRequest request)=>Ok(await payments.CreateAsync(request));
    [HttpGet("payments/{id:guid}")] public async Task<IActionResult> GetPayment(Guid id){var x=await payments.GetAsync(id);return x is null?NotFound():Ok(x);}
}
