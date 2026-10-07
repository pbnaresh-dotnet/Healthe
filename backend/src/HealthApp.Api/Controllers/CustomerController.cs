using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/customer"), Authorize(Roles = "Customer")]
public sealed class CustomerController(ICustomerService service, IOutletPackageService outletPackages) : ControllerBase
{
    [HttpGet("profile")] public async Task<IActionResult> Profile() => Ok(await service.GetProfileAsync());
    [HttpPost("packages/{subscriptionId:guid}/accept")]
    public async Task<IActionResult> AcceptOutletPackage(Guid subscriptionId, AcceptOutletPackageRequest request)
        => Ok(await outletPackages.AcceptAsync(subscriptionId, request,
            new LegalAcceptanceContext(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString())));
    [HttpPatch("marketing-preference")] public async Task<IActionResult> MarketingPreference(UpdateMarketingPreferenceRequest request) => Ok(await service.UpdateMarketingPreferenceAsync(request));
    [HttpGet("legal-status")] public async Task<IActionResult> LegalStatus() => Ok(await service.GetLegalStatusAsync());
    [HttpPost("legal-acceptance")] public async Task<IActionResult> AcceptLegal(AcceptCustomerLegalRequest request)
        => Ok(await service.AcceptLegalAsync(request,
            new LegalAcceptanceContext(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString())));
    [HttpGet("dashboard")] public async Task<IActionResult> Dashboard() => Ok(await service.GetDashboardAsync());
    [HttpGet("subscriptions")] public async Task<IActionResult> Subscriptions() => Ok(await service.GetSubscriptionsAsync());
    [HttpPost("subscriptions/quote")] public async Task<IActionResult> Quote(SubscriptionQuoteRequest request) => Ok(await service.QuoteAsync(request));
    [HttpPost("subscriptions")] public async Task<IActionResult> Subscribe(CreateSubscriptionRequest request)
    => Ok(await service.SubscribeAsync(request, new LegalAcceptanceContext(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString())));
    [HttpGet("subscriptions/{subscriptionId:guid}/recipes")] public async Task<IActionResult> SubscriptionRecipes(Guid subscriptionId, [FromQuery] string? category) => Ok(await service.GetSubscriptionRecipesAsync(subscriptionId, category));
    [HttpGet("subscriptions/{subscriptionId:guid}/menu")] public async Task<IActionResult> SubscriptionMenu(Guid subscriptionId) => Ok(await service.GetSubscriptionMenuAsync(subscriptionId));
    [HttpGet("subscriptions/{subscriptionId:guid}/meal-selections")] public async Task<IActionResult> MealSelections(Guid subscriptionId, [FromQuery] DateTime? weekStart) => Ok(await service.GetMealSelectionsAsync(subscriptionId, weekStart));
    [HttpPut("subscriptions/{subscriptionId:guid}/meal-selections")] public async Task<IActionResult> SaveMealSelections(Guid subscriptionId, SaveMealSelectionsRequest request) => Ok(await service.SaveMealSelectionsAsync(subscriptionId, request));
    [HttpPost("subscriptions/{subscriptionId:guid}/meal-selections/{selectionId:guid}/skip")] public async Task<IActionResult> SkipMeal(Guid subscriptionId, Guid selectionId, SkipMealRequest request) => Ok(await service.SkipMealAsync(subscriptionId, selectionId, request));
    [HttpPost("subscriptions/{subscriptionId:guid}/days/{date:datetime}/skip")] public async Task<IActionResult> SkipDay(Guid subscriptionId, DateTime date, SkipDayRequest request) => Ok(await service.SkipDayAsync(subscriptionId, date, request));
    [HttpPost("subscriptions/{subscriptionId:guid}/meal-selections/{selectionId:guid}/reschedule")] public async Task<IActionResult> RescheduleMeal(Guid subscriptionId, Guid selectionId, RescheduleMealRequest request) => Ok(await service.RescheduleMealAsync(subscriptionId, selectionId, request));
    [HttpGet("credit")] public async Task<IActionResult> Credit() => Ok(await service.GetCreditBalanceAsync());
    [HttpGet("credit/transactions")] public async Task<IActionResult> CreditTransactions() => Ok(await service.GetCreditTransactionsAsync());
    [HttpGet("orders")] public async Task<IActionResult> Orders() => Ok(await service.GetOrdersAsync());
}
