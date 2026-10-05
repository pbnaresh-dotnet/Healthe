using HealthApp.Application.Abstractions;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class OutletSettingsService(
    ICurrentUser current,
    IOutletRepository outlets,
    IUserRepository users,
    IRecipeRepository recipes,
    IMealPlanRepository mealPlans,
    IOutletMenuRepository menu,
    IUserRepository users,
    IOutletDeliveryAreaRepository deliveryAreas,
    IDeliveryPricingRepository pricing) : IOutletSettingsService
{
    private static readonly DayOfWeek[] Weekdays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    public async Task<OutletSettingsDto?> GetAsync()
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId);
        if (outlet is null) return null;
        return new(
            outlet.Id, outlet.Name, outlet.City, outlet.State, outlet.Pincode,
            outlet.DeliveryDays, outlet.RestaurantGstRate, outlet.RestaurantGstMode.ToString(),
            await BuildReadinessAsync(outlet));
    }

    public async Task<OutletSettingsDto?> UpdateDeliveryDaysAsync(UpdateOutletSettingsRequest request)
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");

        var days = ParseDays(request.DeliveryDays);
        if (days.Count == 0)
            throw new ArgumentException("Select at least one delivery day.");

        outlet.DeliveryDays = string.Join(",", Weekdays
            .Where(days.Contains)
            .Select(x => x.ToString()));

        await outlets.UpdateAsync(outlet);
        return await GetAsync();
    }

    public async Task<OutletReadinessDto?> GetReadinessAsync()
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId);
        return outlet is null ? null : await BuildReadinessAsync(outlet);
    }

    public async Task<OutletReadinessDto?> GoLiveAsync()
    {
        if (current.OutletId is not Guid outletId) return null;
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");

        if (outlet.Status == OutletStatus.Suspended)
            throw new InvalidOperationException("A suspended outlet cannot go live.");

        if (outlet.Status != OutletStatus.Active && outlet.Status != OutletStatus.Live)
            throw new InvalidOperationException("The outlet must be activated by Super Admin before it can go live.");

        var user = current.UserId is Guid userId ? await users.FindByIdAsync(userId) : null;
        if (user?.IsDemo == true)
            throw new InvalidOperationException("Demo accounts cannot be published to the customer marketplace.");

        var readiness = await BuildReadinessAsync(outlet);
        if (!readiness.CanGoLive)
            throw new InvalidOperationException("Complete every required setup item before going live.");

        if (outlet.Status != OutletStatus.Live)
        {
            outlet.Status = OutletStatus.Live;
            await outlets.UpdateAsync(outlet);
        }

        return await BuildReadinessAsync(outlet);
    }

    private async Task<OutletReadinessDto> BuildReadinessAsync(HealthApp.Domain.Entities.Outlet outlet)
    {
        var outletId = outlet.Id;
        var activeRecipes = (await recipes.GetByOutletAsync(outletId)).Count(x => x.IsActive);
        var activePlans = (await mealPlans.GetByOutletAsync(outletId)).Count(x => x.IsActive);
        var menuItems = (await menu.GetByOutletAsync(outletId)).Where(x => x.IsAvailable).ToList();
        var activeDrivers = (await users.GetAllAsync()).Count(x =>
            x.OutletId == outletId &&
            x.Role == UserRole.Driver &&
            x.IsActive &&
            (!x.IsDemo || !x.DemoExpiresAtUtc.HasValue || x.DemoExpiresAtUtc.Value > DateTime.UtcNow));
        var selectedAreas = (await deliveryAreas.GetByOutletAsync(outletId)).Count;
        var pricingRules = (await pricing.GetByOutletAsync(outletId)).Count;
        var deliveryDays = ParseDays(outlet.DeliveryDays);

        var menuDaysReady = deliveryDays.Count > 0
            ? deliveryDays.Count(day => menuItems.Any(x => x.DayOfWeek == day))
            : 0;

        var items = new List<OutletReadinessItemDto>
        {
            new("business", "Business profile", "Outlet name, city, state and pincode must be configured.", 
                !string.IsNullOrWhiteSpace(outlet.Name) && !string.IsNullOrWhiteSpace(outlet.City) &&
                !string.IsNullOrWhiteSpace(outlet.State) && !string.IsNullOrWhiteSpace(outlet.Pincode), 1, 1, "business"),
            new("delivery-days", "Delivery days", "Choose the days your kitchen accepts subscription deliveries.",
                deliveryDays.Count > 0, deliveryDays.Count, 1, "delivery-days"),
            new("recipes", "Recipes", "Add at least one active recipe to your outlet menu.",
                activeRecipes > 0, activeRecipes, 1, "recipes"),
            new("meal-plans", "Meal plans", "Create at least one active customer meal plan.",
                activePlans > 0, activePlans, 1, "meal-plans"),
            new("menu", "Weekly menu", "Every selected delivery day must have at least one available menu item.",
                deliveryDays.Count > 0 && menuDaysReady == deliveryDays.Count, menuDaysReady, Math.Max(1, deliveryDays.Count), "menu"),
            new("drivers", "Drivers", "Add at least one active delivery driver.",
                activeDrivers > 0, activeDrivers, 1, "drivers"),
            new("delivery-areas", "Delivery areas", "Select at least one approved city delivery area.",
                selectedAreas > 0, selectedAreas, 1, "delivery-areas"),
            new("delivery-pricing", "Delivery pricing", "Configure at least one distance-based delivery fee.",
                pricingRules > 0, pricingRules, 1, "pricing"),
            new("tax", "Tax settings", "Confirm the restaurant GST rate for customer pricing.",
                outlet.RestaurantGstRate >= 0m, 1, 1, "tax")
        };

        var currentUser = current.UserId is Guid userId ? await users.FindByIdAsync(userId) : null;
        var isDemo = currentUser?.IsDemo == true;
        var canGoLive = !isDemo && (outlet.Status == OutletStatus.Live || items.All(x => x.IsComplete));
        var missing = items.Where(x => !x.IsComplete).Select(x => x.Title).ToList();
        var message = outlet.Status == OutletStatus.Live
            ? "Outlet is live and available to customers."
            : outlet.Status == OutletStatus.Active
                ? isDemo ? "Demo accounts can explore the full workspace but are not published to customers." : canGoLive ? "All required setup is complete. Your outlet is ready to go live." : $"Complete: {string.Join(", ", missing)}."
                : "Waiting for Super Admin activation.";

        return new(outlet.Status.ToString(), outlet.Status == OutletStatus.Live, canGoLive, items, message);
    }

    private static HashSet<DayOfWeek> ParseDays(string? value)
    {
        var result = new HashSet<DayOfWeek>();
        foreach (var token in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<DayOfWeek>(token, true, out var day))
                result.Add(day);
        return result;
    }
}
