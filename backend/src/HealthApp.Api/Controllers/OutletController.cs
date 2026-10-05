using HealthApp.Application.Abstractions; using HealthApp.Shared.DTOs; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/outlets/me"),Authorize(Roles="OutletAdmin")]
public sealed class OutletController(IOutletService service, IFileStorage fileStorage, IOutletPackageService outletPackages, IOutletSettingsService settings, ICurrentUser current):ControllerBase
{ [HttpGet("settings")] public async Task<IActionResult> Settings()=>Ok(await settings.GetAsync()); [HttpPut("settings/branding")] public async Task<IActionResult> UpdateBranding(UpdateOutletBrandingRequest request)=>Ok(await settings.UpdateBrandingAsync(request)); [HttpPost("settings/branding/assets")] [RequestSizeLimit(5_000_000)] public async Task<IActionResult> UploadBrandingAsset([FromForm] string assetType, IFormFile file)
{
    if (current.OutletId is not Guid outletId)
        return Unauthorized(new { message = "The current user is not associated with an outlet." });
    if (file is null || file.Length == 0)
        return BadRequest(new { message = "Please select an image." });
    if (file.Length > 5_000_000)
        return BadRequest(new { message = "Image must be 5 MB or smaller." });

    var type = (assetType ?? string.Empty).Trim().ToLowerInvariant();
    if (type is not ("logo" or "hero" or "favicon"))
        return BadRequest(new { message = "Supported branding assets are: logo, hero and favicon." });

    var extensions = type == "favicon"
        ? new[] { ".png", ".ico", ".jpg", ".jpeg", ".webp" }
        : new[] { ".jpg", ".jpeg", ".png", ".webp" };
    var ext = Path.GetExtension(file.FileName);
    if (!extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        return BadRequest(new { message = type == "favicon" ? "Favicon formats: PNG, ICO, JPG and WEBP." : "Supported formats: JPG, PNG and WEBP." });
    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        return BadRequest(new { message = "Only image files are allowed." });

    await using var stream = file.OpenReadStream();
    var stored = await fileStorage.UploadAsync(
        stream,
        file.FileName,
        file.ContentType,
        $"outlets/{outletId:N}/branding/{type}",
        HttpContext.RequestAborted);
    return Ok(await settings.UpdateBrandingAssetAsync(type, stored.Url));
} [HttpGet("settings/readiness")] public async Task<IActionResult> SettingsReadiness()=>Ok(await settings.GetReadinessAsync()); [HttpPut("settings/delivery-days")] public async Task<IActionResult> UpdateDeliveryDays(UpdateOutletSettingsRequest request)=>Ok(await settings.UpdateDeliveryDaysAsync(request)); [HttpPost("settings/go-live")] public async Task<IActionResult> GoLive()=>Ok(await settings.GoLiveAsync()); [HttpGet] public async Task<IActionResult> Me()=>Ok(await service.GetCurrentAsync()); [HttpGet("tax-settings")] public async Task<IActionResult> TaxSettings()=>Ok(await service.GetTaxSettingsAsync()); [HttpPut("tax-settings")] public async Task<IActionResult> UpdateTaxSettings(UpdateOutletTaxSettingsRequest r)=>Ok(await service.UpdateTaxSettingsAsync(r)); [HttpGet("dashboard")] public async Task<IActionResult> Dashboard()=>Ok(await service.GetDashboardAsync()); [HttpGet("subscriptions/{subscriptionId:guid}")] public async Task<IActionResult> SubscriptionDetail(Guid subscriptionId)=>await service.GetSubscriptionDetailAsync(subscriptionId) is { } result?Ok(result):NotFound(); [HttpGet("kitchen")] public async Task<IActionResult> Kitchen([FromQuery]DateTime? date)=>Ok(await service.GetKitchenDayAsync((date??DateTime.UtcNow).Date)); [HttpGet("billing")] public async Task<IActionResult> Billing()=>Ok(await service.GetBillingAsync()); [HttpGet("subscription/plans")] public async Task<IActionResult> SubscriptionPlans()=>Ok(await service.GetSaaSPlansAsync()); [HttpPut("subscription")] public async Task<IActionResult> ChangeSubscription(ChangeOutletSubscriptionRequest r)=>Ok(await service.ChangeSubscriptionAsync(r)); [HttpGet("meal-plans")] public async Task<IActionResult> Plans()=>Ok(await service.GetPlansAsync()); [HttpPost("meal-plans")] public async Task<IActionResult> CreatePlan(CreateMealPlanRequest r)=>Ok(await service.CreatePlanAsync(r)); [HttpGet("recipes")] public async Task<IActionResult> Recipes([FromQuery]string? category)=>Ok(await service.GetRecipesAsync(category)); [HttpGet("menu")] public async Task<IActionResult> Menu()=>Ok(await service.GetMenuAsync()); [HttpPut("menu")] public async Task<IActionResult> SaveMenu(BulkMenuRequest r)=>Ok(await service.SaveMenuAsync(r)); [HttpPost("recipes")] public async Task<IActionResult> CreateRecipe(CreateRecipeRequest r)=>Ok(await service.CreateRecipeAsync(r)); [HttpPost("recipes/image")] [RequestSizeLimit(5_000_000)] public async Task<IActionResult> UploadRecipeImage(IFormFile file)
{
    if (file is null || file.Length == 0) return BadRequest(new { message = "Please select an image." });
    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Only image files are allowed." });
    if (file.Length > 5_000_000) return BadRequest(new { message = "Image must be 5 MB or smaller." });
    var ext = Path.GetExtension(file.FileName);
    var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
    if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = "Supported formats: JPG, PNG and WEBP." });
    if (current.OutletId is not Guid outletId)
        return Unauthorized(new { message = "The current user is not associated with an outlet." });

    await using var stream = file.OpenReadStream();
    var stored = await fileStorage.UploadAsync(
        stream,
        file.FileName,
        file.ContentType,
        $"outlets/{outletId:N}/recipes",
        HttpContext.RequestAborted);
    return Ok(new { url = stored.Url, key = stored.Key, contentType = stored.ContentType });
} [HttpPut("recipes/{recipeId:guid}")] public async Task<IActionResult> UpdateRecipe(Guid recipeId,UpdateRecipeRequest r){var result=await service.UpdateRecipeAsync(recipeId,r);return result is null?NotFound():Ok(result);} [HttpDelete("recipes/{recipeId:guid}")] public async Task<IActionResult> DeleteRecipe(Guid recipeId)=>await service.DeleteRecipeAsync(recipeId)?NoContent():NotFound(); [HttpGet("customers")] public async Task<IActionResult> Customers()=>Ok(await outletPackages.GetCustomersAsync());
 [HttpPost("customers")] public async Task<IActionResult> CreateCustomer(CreateOutletCustomerRequest request)=>Ok(await outletPackages.CreateCustomerAsync(request));
 [HttpGet("customers/{customerId:guid}/profile")] public async Task<IActionResult> CustomerProfile(Guid customerId)=>Ok(await outletPackages.GetCustomerProfileAsync(customerId));
 [HttpPut("customers/{customerId:guid}/profile")] public async Task<IActionResult> UpdateCustomerProfile(Guid customerId, SaveCustomerProfileRequest request)=>Ok(await outletPackages.UpdateCustomerProfileAsync(customerId, request));
 [HttpGet("customers/{customerId:guid}/addresses")] public async Task<IActionResult> CustomerAddresses(Guid customerId)=>Ok(await outletPackages.GetCustomerAddressesAsync(customerId));
 [HttpPost("customers/{customerId:guid}/addresses")] public async Task<IActionResult> CreateCustomerAddress(Guid customerId, OutletPackageAddressRequest request)=>Ok(await outletPackages.CreateCustomerAddressAsync(customerId, request));
 [HttpPost("packages/quote")] public async Task<IActionResult> QuotePackage(OutletPackageQuoteRequest request)=>Ok(await outletPackages.QuoteAsync(request));
 [HttpPost("packages")] public async Task<IActionResult> CreatePackage(CreateOutletPackageRequest request)=>Ok(await outletPackages.CreateAsync(request));
 [HttpPost("packages/{subscriptionId:guid}/mark-paid")] public async Task<IActionResult> MarkPackagePaid(Guid subscriptionId, MarkOutletPackagePaidRequest request)=>Ok(await outletPackages.MarkPaidAsync(subscriptionId, request)); [HttpGet("subscriptions")] public async Task<IActionResult> Subscriptions()=>Ok(await service.GetSubscriptionsAsync()); [HttpGet("orders")] public async Task<IActionResult> Orders()=>Ok(await service.GetOrdersAsync()); [HttpGet("deliveries")] public async Task<IActionResult> Deliveries()=>Ok(await service.GetDeliveriesAsync()); }
