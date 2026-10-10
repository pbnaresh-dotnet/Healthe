using HealthApp.Application.Abstractions; using HealthApp.Shared.DTOs; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/outlets/me"),Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff,Driver")]
public sealed class OutletController(IOutletService service, IFileStorage fileStorage, IMediaService mediaService, IOutletPackageService outletPackages, IOutletSettingsService settings, ICurrentUser current, IIngredientConsumptionService ingredientConsumption):ControllerBase
{
 [Authorize(Roles="OutletAdmin")][HttpGet("settings")] public async Task<IActionResult> Settings()=>Ok(await settings.GetAsync());
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("settings/manager")] public async Task<IActionResult> ManagerSettings()
 {
     var value=await settings.GetAsync();
     if(value is null) return NotFound();
     return Ok(new
     {
         outletId=value.OutletId,
         outletName=value.OutletName,
         city=value.City,
         state=value.State,
         deliveryDays=value.DeliveryDays,
         deliveryCoverageMode=value.DeliveryCoverageMode,
         serviceRadiusKm=value.ServiceRadiusKm,
         latitude=value.Latitude,
         longitude=value.Longitude
     });
 }
 [Authorize(Roles="OutletAdmin")][HttpGet("settings/domains")] public async Task<IActionResult> Domains()=>Ok(await settings.GetDomainsAsync());
 [Authorize(Roles="OutletAdmin")][HttpPost("settings/domains/{id:guid}/verify")] public async Task<IActionResult> VerifyDomain(Guid id, VerifyOutletDomainRequest request)
 {
     try { return Ok(await settings.VerifyDomainAsync(id, request.ActivateIfReady)); }
     catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
     catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
 }
 [Authorize(Roles="OutletAdmin")][HttpPost("settings/domains")] public async Task<IActionResult> RequestDomain(RequestOutletDomainRequest request)
 {
     try { return Ok(await settings.RequestDomainAsync(request)); }
     catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
     catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
 } [Authorize(Roles="OutletAdmin")][HttpPut("settings/branding")] public async Task<IActionResult> UpdateBranding(UpdateOutletBrandingRequest request)=>Ok(await settings.UpdateBrandingAsync(request)); [Authorize(Roles="OutletAdmin")][HttpPost("settings/branding/assets")] [RequestSizeLimit(10_000_000)] public async Task<IActionResult> UploadBrandingAsset([FromForm] string assetType, IFormFile file)
{
    if (current.OutletId is not Guid outletId)
        return Unauthorized(new { message = "The current user is not associated with an outlet." });
    if (file is null || file.Length == 0)
        return BadRequest(new { message = "Please select an image." });
    if (file.Length > 10_000_000)
        return BadRequest(new { message = "Image must be 10 MB or smaller. It will be resized and compressed automatically." });

    var type = (assetType ?? string.Empty).Trim().ToLowerInvariant();
    if (type is not ("logo" or "hero" or "favicon"))
        return BadRequest(new { message = "Supported branding assets are: logo, hero and favicon." });

    var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
    var ext = Path.GetExtension(file.FileName);
    if (!extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        return BadRequest(new { message = "Supported image formats: JPG, PNG and WEBP." });
    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        return BadRequest(new { message = "Only image files are allowed." });

    await using var stream = file.OpenReadStream();
    var profile = type switch
    {
        "logo" => MediaImageProfile.OutletLogo,
        "hero" => MediaImageProfile.OutletHero,
        "favicon" => MediaImageProfile.Favicon,
        _ => MediaImageProfile.OutletHero
    };
    var stored = await mediaService.UploadImageAsync(
        stream,
        file.FileName,
        file.ContentType,
        $"outlets/{outletId:N}/branding/{type}",
        profile,
        HttpContext.RequestAborted);

    var updated = await settings.UpdateBrandingAssetAsync(type, stored.Url);
    return Ok(new
    {
        brandName = updated?.BrandName,
        tagline = updated?.Tagline,
        logoUrl = updated?.LogoUrl,
        heroImageUrl = updated?.HeroImageUrl,
        faviconUrl = updated?.FaviconUrl,
        thumbnailUrl = stored.Thumbnail?.Url,
        smallUrl = stored.Small?.Url,
        mediumUrl = stored.Medium?.Url,
        largeUrl = stored.Large?.Url,
        originalSizeBytes = stored.OriginalSizeBytes,
        originalWidth = stored.OriginalWidth,
        originalHeight = stored.OriginalHeight
    });
} [Authorize(Roles="OutletAdmin")][HttpGet("settings/readiness")] public async Task<IActionResult> SettingsReadiness()=>Ok(await settings.GetReadinessAsync());
    [Authorize(Roles="OutletAdmin")][HttpGet("settings/legal")] public async Task<IActionResult> Legal()=>Ok(await settings.GetLegalPoliciesAsync());
    [Authorize(Roles="OutletAdmin")][HttpPut("settings/legal")] public async Task<IActionResult> UpdateLegal(UpdateOutletLegalPoliciesRequest request)=>Ok(await settings.UpdateLegalPoliciesAsync(request)); [Authorize(Roles="OutletAdmin,OutletManager")][HttpPut("settings/delivery-days")] public async Task<IActionResult> UpdateDeliveryDays(UpdateOutletSettingsRequest request)=>Ok(await settings.UpdateDeliveryDaysAsync(request)); [Authorize(Roles="OutletAdmin")][HttpPut("settings/package-settings")] public async Task<IActionResult> UpdatePackageSettings(UpdateOutletPackageSettingsRequest request)=>Ok(await settings.UpdatePackageSettingsAsync(request)); [Authorize(Roles="OutletAdmin")][HttpPut("settings/late-skip-fee")] public async Task<IActionResult> UpdateLateSkipFee(UpdateOutletLateSkipFeeRequest request)=>Ok(await settings.UpdateLateSkipFeeAsync(request)); [Authorize(Roles="OutletAdmin")][HttpPost("settings/go-live")] public async Task<IActionResult> GoLive()=>Ok(await settings.GoLiveAsync()); [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpGet] public async Task<IActionResult> Me()=>Ok(await service.GetCurrentAsync()); [Authorize(Roles="OutletAdmin")][HttpGet("tax-settings")] public async Task<IActionResult> TaxSettings()=>Ok(await service.GetTaxSettingsAsync()); [Authorize(Roles="OutletAdmin")][HttpPut("tax-settings")] public async Task<IActionResult> UpdateTaxSettings(UpdateOutletTaxSettingsRequest r)=>Ok(await service.UpdateTaxSettingsAsync(r)); [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("dashboard")] public async Task<IActionResult> Dashboard()=>Ok(await service.GetDashboardAsync()); [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("subscriptions/{subscriptionId:guid}")] public async Task<IActionResult> SubscriptionDetail(Guid subscriptionId)=>await service.GetSubscriptionDetailAsync(subscriptionId) is { } result?Ok(result):NotFound(); [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpGet("kitchen")] public async Task<IActionResult> Kitchen([FromQuery]DateTime? date)=>Ok(await service.GetKitchenDayAsync((date??DateTime.UtcNow).Date)); [Authorize(Roles="OutletAdmin")][HttpGet("billing")] public async Task<IActionResult> Billing()=>Ok(await service.GetBillingAsync()); [Authorize(Roles="OutletAdmin")][HttpGet("subscription/plans")] public async Task<IActionResult> SubscriptionPlans()=>Ok(await service.GetSaaSPlansAsync()); [Authorize(Roles="OutletAdmin")][HttpPut("subscription")] public async Task<IActionResult> ChangeSubscription(ChangeOutletSubscriptionRequest r)=>Ok(await service.ChangeSubscriptionAsync(r)); [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("meal-plans")] public async Task<IActionResult> Plans()=>Ok(await service.GetPlansAsync()); [Authorize(Roles="OutletAdmin,OutletManager")][HttpPost("meal-plans")] public async Task<IActionResult> CreatePlan(CreateMealPlanRequest r)=>Ok(await service.CreatePlanAsync(r)); [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpGet("recipes")] public async Task<IActionResult> Recipes([FromQuery]string? category,[FromQuery]string? search,[FromQuery]int? page=null,[FromQuery]int? pageSize=null)=>page.HasValue||pageSize.HasValue?Ok(await service.GetRecipesPageAsync(category,search,page??1,pageSize??25)):Ok(ListQuery.Apply(await service.GetRecipesAsync(category),search,null,null)); [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpGet("menu")] public async Task<IActionResult> Menu()=>Ok(await service.GetMenuAsync()); [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpPut("menu")] public async Task<IActionResult> SaveMenu(BulkMenuRequest r)=>Ok(await service.SaveMenuAsync(r)); [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpPost("recipes")] public async Task<IActionResult> CreateRecipe(CreateRecipeRequest r)=>Ok(await service.CreateRecipeAsync(r)); [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpPost("recipes/image")] [RequestSizeLimit(10_000_000)] public async Task<IActionResult> UploadRecipeImage(IFormFile file)
{
    if (file is null || file.Length == 0) return BadRequest(new { message = "Please select an image." });
    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Only image files are allowed." });
    if (file.Length > 10_000_000) return BadRequest(new { message = "Image must be 10 MB or smaller. It will be resized and compressed automatically." });
    var ext = Path.GetExtension(file.FileName);
    var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
    if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = "Supported formats: JPG, PNG and WEBP." });
    if (current.OutletId is not Guid outletId)
        return Unauthorized(new { message = "The current user is not associated with an outlet." });

    await using var stream = file.OpenReadStream();
    var stored = await mediaService.UploadImageAsync(
        stream,
        file.FileName,
        file.ContentType,
        $"outlets/{outletId:N}/recipes",
        MediaImageProfile.Recipe,
        HttpContext.RequestAborted);
    return Ok(new
    {
        url = stored.Url,
        key = stored.Key,
        contentType = stored.ContentType,
        thumbnailUrl = stored.Thumbnail?.Url,
        smallUrl = stored.Small?.Url,
        mediumUrl = stored.Medium?.Url,
        largeUrl = stored.Large?.Url,
        originalSizeBytes = stored.OriginalSizeBytes,
        originalWidth = stored.OriginalWidth,
        originalHeight = stored.OriginalHeight
    });
} [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpPut("recipes/{recipeId:guid}")] public async Task<IActionResult> UpdateRecipe(Guid recipeId,UpdateRecipeRequest r){var result=await service.UpdateRecipeAsync(recipeId,r);return result is null?NotFound():Ok(result);} [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpDelete("recipes/{recipeId:guid}")] public async Task<IActionResult> DeleteRecipe(Guid recipeId)=>await service.DeleteRecipeAsync(recipeId)?NoContent():NotFound(); [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("customers")] public async Task<IActionResult> Customers([FromQuery]string? search,[FromQuery]int? page=null,[FromQuery]int? pageSize=null){if(page.HasValue||pageSize.HasValue)return Ok(await outletPackages.GetCustomersPageAsync(search,page??1,pageSize??25));return Ok(ListQuery.Apply(await outletPackages.GetCustomersAsync(),search,null,null));}
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpPost("customers")] public async Task<IActionResult> CreateCustomer(CreateOutletCustomerRequest request)=>Ok(await outletPackages.CreateCustomerAsync(request));
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("customers/{customerId:guid}/profile")] public async Task<IActionResult> CustomerProfile(Guid customerId)=>Ok(await outletPackages.GetCustomerProfileAsync(customerId));
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpPut("customers/{customerId:guid}/profile")] public async Task<IActionResult> UpdateCustomerProfile(Guid customerId, SaveCustomerProfileRequest request)=>Ok(await outletPackages.UpdateCustomerProfileAsync(customerId, request));
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("customers/{customerId:guid}/addresses")] public async Task<IActionResult> CustomerAddresses(Guid customerId)=>Ok(await outletPackages.GetCustomerAddressesAsync(customerId));
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpPost("customers/{customerId:guid}/addresses")] public async Task<IActionResult> CreateCustomerAddress(Guid customerId, OutletPackageAddressRequest request)=>Ok(await outletPackages.CreateCustomerAddressAsync(customerId, request));
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpPost("packages/quote")] public async Task<IActionResult> QuotePackage(OutletPackageQuoteRequest request)=>Ok(await outletPackages.QuoteAsync(request));
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpPost("packages")] public async Task<IActionResult> CreatePackage(CreateOutletPackageRequest request)=>Ok(await outletPackages.CreateAsync(request));
 [Authorize(Roles="OutletAdmin,OutletManager")][HttpPost("packages/{subscriptionId:guid}/mark-paid")] public async Task<IActionResult> MarkPackagePaid(Guid subscriptionId, MarkOutletPackagePaidRequest request)=>Ok(await outletPackages.MarkPaidAsync(subscriptionId, request)); [Authorize(Roles="OutletAdmin,OutletManager")][HttpPost("packages/{subscriptionId:guid}/confirm")] public async Task<IActionResult> ConfirmCustomerPackage(Guid subscriptionId, ConfirmCustomerPackageRequest request){try{return Ok(await outletPackages.ConfirmCustomerPackageAsync(subscriptionId,request));}catch(KeyNotFoundException ex){return NotFound(new{message=ex.Message});}catch(ArgumentException ex){return BadRequest(new{message=ex.Message});}catch(InvalidOperationException ex){return Conflict(new{message=ex.Message});}} [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("subscriptions")] public async Task<IActionResult> Subscriptions([FromQuery]string? status,[FromQuery]string? search,[FromQuery]int? page=null,[FromQuery]int? pageSize=null)=>Ok(ListQuery.Apply(await service.GetSubscriptionsAsync(),search,page,pageSize,status)); [Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")][HttpGet("reports/ingredient-consumption")] public async Task<IActionResult> IngredientConsumption([FromQuery]DateTime? date)=>Ok(await ingredientConsumption.GetDailyAsync((date??DateTime.UtcNow).Date)); [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("orders")] public async Task<IActionResult> Orders([FromQuery]string? status,[FromQuery]string? search,[FromQuery]int? page=null,[FromQuery]int? pageSize=null)=>Ok(ListQuery.Apply(await service.GetOrdersAsync(),search,page,pageSize,status)); [Authorize(Roles="OutletAdmin,OutletManager")][HttpGet("deliveries")] public async Task<IActionResult> Deliveries([FromQuery]string? status,[FromQuery]DateTime? date,[FromQuery]string? search,[FromQuery]int? page=null,[FromQuery]int? pageSize=null)=>Ok(ListQuery.Apply(await service.GetDeliveriesAsync(),search,page,pageSize,status,date)); }
