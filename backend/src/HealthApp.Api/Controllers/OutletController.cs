using HealthApp.Application.Abstractions; using HealthApp.Shared.DTOs; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/outlets/me"),Authorize(Roles="OutletAdmin")]
public sealed class OutletController(IOutletService service, IWebHostEnvironment environment):ControllerBase
{ [HttpGet] public async Task<IActionResult> Me()=>Ok(await service.GetCurrentAsync()); [HttpGet("dashboard")] public async Task<IActionResult> Dashboard()=>Ok(await service.GetDashboardAsync()); [HttpGet("subscriptions/{subscriptionId:guid}")] public async Task<IActionResult> SubscriptionDetail(Guid subscriptionId)=>await service.GetSubscriptionDetailAsync(subscriptionId) is { } result?Ok(result):NotFound(); [HttpGet("kitchen")] public async Task<IActionResult> Kitchen([FromQuery]DateTime? date)=>Ok(await service.GetKitchenDayAsync((date??DateTime.UtcNow).Date)); [HttpGet("billing")] public async Task<IActionResult> Billing()=>Ok(await service.GetBillingAsync()); [HttpGet("subscription/plans")] public async Task<IActionResult> SubscriptionPlans()=>Ok(await service.GetSaaSPlansAsync()); [HttpPut("subscription")] public async Task<IActionResult> ChangeSubscription(ChangeOutletSubscriptionRequest r)=>Ok(await service.ChangeSubscriptionAsync(r)); [HttpGet("meal-plans")] public async Task<IActionResult> Plans()=>Ok(await service.GetPlansAsync()); [HttpPost("meal-plans")] public async Task<IActionResult> CreatePlan(CreateMealPlanRequest r)=>Ok(await service.CreatePlanAsync(r)); [HttpGet("recipes")] public async Task<IActionResult> Recipes([FromQuery]string? category)=>Ok(await service.GetRecipesAsync(category)); [HttpGet("menu")] public async Task<IActionResult> Menu()=>Ok(await service.GetMenuAsync()); [HttpPut("menu")] public async Task<IActionResult> SaveMenu(BulkMenuRequest r)=>Ok(await service.SaveMenuAsync(r)); [HttpPost("recipes")] public async Task<IActionResult> CreateRecipe(CreateRecipeRequest r)=>Ok(await service.CreateRecipeAsync(r)); [HttpPost("recipes/image")] [RequestSizeLimit(5_000_000)] public async Task<IActionResult> UploadRecipeImage(IFormFile file)
{
    if (file is null || file.Length == 0) return BadRequest(new { message = "Please select an image." });
    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Only image files are allowed." });
    if (file.Length > 5_000_000) return BadRequest(new { message = "Image must be 5 MB or smaller." });
    var ext = Path.GetExtension(file.FileName);
    var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
    if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = "Supported formats: JPG, PNG and WEBP." });
    var root = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
    var folder = Path.Combine(root, "uploads", "recipes");
    Directory.CreateDirectory(folder);
    var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
    await using var stream = System.IO.File.Create(Path.Combine(folder, name));
    await file.CopyToAsync(stream);
    return Ok(new { url = $"/uploads/recipes/{name}" });
} [HttpPut("recipes/{recipeId:guid}")] public async Task<IActionResult> UpdateRecipe(Guid recipeId,UpdateRecipeRequest r){var result=await service.UpdateRecipeAsync(recipeId,r);return result is null?NotFound():Ok(result);} [HttpDelete("recipes/{recipeId:guid}")] public async Task<IActionResult> DeleteRecipe(Guid recipeId)=>await service.DeleteRecipeAsync(recipeId)?NoContent():NotFound(); [HttpGet("customers")] public async Task<IActionResult> Customers()=>Ok(await service.GetCustomersAsync()); [HttpGet("subscriptions")] public async Task<IActionResult> Subscriptions()=>Ok(await service.GetSubscriptionsAsync()); [HttpGet("orders")] public async Task<IActionResult> Orders()=>Ok(await service.GetOrdersAsync()); [HttpGet("deliveries")] public async Task<IActionResult> Deliveries()=>Ok(await service.GetDeliveriesAsync()); }
