using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace HealthApp.Infrastructure.Repositories;

public abstract class EfRepository(HealthAppDbContext Db)
{
    protected HealthAppDbContext Context => Db;
    protected Task<int> SaveAsync(CancellationToken ct = default) => Db.SaveChangesAsync(ct);
}

public sealed class UserRepository(HealthAppDbContext db) : EfRepository(db), IUserRepository
{
    public Task<User?> FindByEmailAsync(string email) => db.Users.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLower());
    public Task<User?> FindByIdAsync(Guid id) => db.Users.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddAsync(User user) {
        db.Users.Add(user);
        await SaveAsync();
    }
    public async Task<IReadOnlyList<User>> GetAllAsync() => await db.Users.AsNoTracking().OrderBy(x => x.Email).ToListAsync();
}

public sealed class OutletRepository(HealthAppDbContext db) : EfRepository(db), IOutletRepository
{
    public async Task<IReadOnlyList<Outlet>> GetAllAsync() => await db.Outlets.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
    public Task<Outlet?> GetByIdAsync(Guid id) => db.Outlets.FirstOrDefaultAsync(x => x.Id == id);
    public Task<Outlet?> GetBySlugAsync(string slug) => db.Outlets.FirstOrDefaultAsync(x => x.Slug == slug.Trim().ToLower());
    public Task<Outlet?> GetBySubdomainAsync(string subdomain) => db.Outlets.FirstOrDefaultAsync(x => x.Subdomain == subdomain.Trim().ToLower());
    public async Task AddAsync(Outlet outlet) {
        db.Outlets.Add(outlet);
        await SaveAsync();
    }
}

public sealed class SaaSPlanRepository(HealthAppDbContext db) : EfRepository(db), ISaaSPlanRepository
{
    public async Task<IReadOnlyList<SaaSPlan>> GetActiveAsync() => await db.SaaSPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.MonthlyFee).ToListAsync();
    public Task<SaaSPlan?> GetAsync(Guid id) => db.SaaSPlans.FirstOrDefaultAsync(x => x.Id == id);
}

public sealed class OutletSubscriptionRepository(HealthAppDbContext db) : EfRepository(db), IOutletSubscriptionRepository
{
    public Task<OutletSubscription?> GetByOutletAsync(Guid outletId) => db.OutletSubscriptions.FirstOrDefaultAsync(x => x.OutletId == outletId && x.Status == "Active");
    public async Task AddAsync(OutletSubscription subscription) {
        db.OutletSubscriptions.Add(subscription);
        await SaveAsync();
    }
    public async Task UpdateAsync(OutletSubscription subscription) {
        db.OutletSubscriptions.Update(subscription);
        await SaveAsync();
    }
}

public sealed class PlatformTransactionRepository(HealthAppDbContext db) : EfRepository(db), IPlatformTransactionRepository
{
    public async Task AddAsync(PlatformTransaction transaction) {
        db.PlatformTransactions.Add(transaction);
        await SaveAsync();
    }
    public async Task<IReadOnlyList<PlatformTransaction>> GetAllAsync() => await db.PlatformTransactions.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync();
    public Task<bool> ExistsByReferenceAsync(string referenceId) => db.PlatformTransactions.AnyAsync(x => x.ReferenceId == referenceId);
}

public sealed class MealPlanRepository(HealthAppDbContext db) : EfRepository(db), IMealPlanRepository
{
    public async Task<IReadOnlyList<MealPlan>> GetByOutletAsync(Guid outletId) => await db.MealPlans.AsNoTracking().Where(x => x.OutletId == outletId).OrderBy(x => x.Name).ToListAsync();
    public Task<MealPlan?> GetAsync(Guid id) => db.MealPlans.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddAsync(MealPlan plan) {
        db.MealPlans.Add(plan);
        await SaveAsync();
    }
}

public sealed class RecipeRepository(HealthAppDbContext db) : EfRepository(db), IRecipeRepository
{
    private IQueryable<Recipe> Details(IQueryable<Recipe> query) => query
    .Include(x => x.RecipeIngredients).ThenInclude(x => x.Ingredient).ThenInclude(x => x.Allergens).ThenInclude(x => x.Allergen)
    .Include(x => x.RecipeAllergens).ThenInclude(x => x.Allergen);
    public async Task<IReadOnlyList<Recipe>> GetByOutletAsync(Guid outletId) => await Details(db.Recipes.AsNoTracking().Where(x => x.OutletId == outletId)).OrderBy(x => x.Name).ToListAsync();
    public async Task<IReadOnlyList<Recipe>> GetByOutletAndCategoryAsync(Guid outletId, string? category)
    {
        var q = Details(db.Recipes.AsNoTracking().Where(x => x.OutletId == outletId));
        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<RecipeCategory>(category, true, out var parsed)) q = q.Where(x => x.Category == parsed);
        return await q.OrderBy(x => x.Name).ToListAsync();
    }
    public Task<Recipe?> GetAsync(Guid id) => Details(db.Recipes.Where(x => x.Id == id)).FirstOrDefaultAsync();
    public async Task AddAsync(Recipe recipe) {
        db.Recipes.Add(recipe);
        await SaveAsync();
    }
    public async Task UpdateAsync(Recipe recipe) {
        db.Recipes.Update(recipe);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid id)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(x => x.Id == id);
        if (recipe is null) return;
        db.Recipes.Remove(recipe);
        await SaveAsync();
    }
}

public sealed class OutletMenuRepository(HealthAppDbContext db) : EfRepository(db), IOutletMenuRepository
{
    public async Task<IReadOnlyList<OutletMenuItem>> GetByOutletAsync(Guid outletId) => await db.OutletMenuItems.AsNoTracking().Where(x => x.OutletId == outletId).OrderBy(x => x.DayOfWeek).ThenBy(x => x.MealSlot).ThenBy(x => x.DisplayOrder).ToListAsync();
    public async Task<IReadOnlyList<OutletMenuItem>> GetByOutletDayAsync(Guid outletId, DayOfWeek day) => await db.OutletMenuItems.AsNoTracking().Where(x => x.OutletId == outletId && x.DayOfWeek == day && x.IsAvailable).OrderBy(x => x.MealSlot).ThenBy(x => x.DisplayOrder).ToListAsync();
    public async Task AddAsync(OutletMenuItem item) {
        db.OutletMenuItems.Add(item);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid id) {
        var item = await db.OutletMenuItems.FindAsync(id);
        if (item is not null) {
            db.OutletMenuItems.Remove(item);
            await SaveAsync();
        }
    }
    public async Task ReplaceAsync(Guid outletId, IEnumerable<OutletMenuItem> items)
    {
        var existing = await db.OutletMenuItems.Where(x => x.OutletId == outletId).ToListAsync();
        db.OutletMenuItems.RemoveRange(existing);
        db.OutletMenuItems.AddRange(items);
        await SaveAsync();
    }
}

public sealed class SubscriptionRepository(HealthAppDbContext db) : EfRepository(db), ISubscriptionRepository
{
    public async Task<IReadOnlyList<Subscription>> GetByCustomerAsync(Guid id) => await db.Subscriptions.AsNoTracking().Where(x => x.CustomerId == id).OrderByDescending(x => x.StartDate).ToListAsync();
    public async Task<IReadOnlyList<Subscription>> GetByOutletAsync(Guid id) => await db.Subscriptions.AsNoTracking().Where(x => x.OutletId == id).OrderByDescending(x => x.StartDate).ToListAsync();
    public Task<Subscription?> GetAsync(Guid id) => db.Subscriptions.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddAsync(Subscription s) {
        db.Subscriptions.Add(s);
        await SaveAsync();
    }
}

public sealed class SubscriptionMealSelectionRepository(HealthAppDbContext db) : EfRepository(db), ISubscriptionMealSelectionRepository
{
    public async Task<IReadOnlyList<SubscriptionMealSelection>> GetBySubscriptionAsync(Guid id) => await db.SubscriptionMealSelections.AsNoTracking().Where(x => x.SubscriptionId == id).OrderBy(x => x.MealDate).ThenBy(x => x.MealSlot).ToListAsync();
    public async Task<IReadOnlyList<SubscriptionMealSelection>> GetBySubscriptionAndDateRangeAsync(Guid id, DateTime from, DateTime to) => await db.SubscriptionMealSelections.AsNoTracking().Where(x => x.SubscriptionId == id && x.MealDate >= from && x.MealDate < to).OrderBy(x => x.MealDate).ThenBy(x => x.MealSlot).ToListAsync();
    public Task<SubscriptionMealSelection?> GetAsync(Guid id) => db.SubscriptionMealSelections.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddRangeAsync(IEnumerable<SubscriptionMealSelection> selections) {
        db.SubscriptionMealSelections.AddRange(selections);
        await SaveAsync();
    }
    public async Task UpdateAsync(SubscriptionMealSelection selection) {
        db.SubscriptionMealSelections.Update(selection);
        await SaveAsync();
    }
    public async Task DeleteBySubscriptionAndDateRangeAsync(Guid id, DateTime from, DateTime to) {
        var rows = await db.SubscriptionMealSelections.Where(x => x.SubscriptionId == id && x.MealDate >= from && x.MealDate < to).ToListAsync();
        db.SubscriptionMealSelections.RemoveRange(rows);
        await SaveAsync();
    }
}

public sealed class CustomerCreditRepository(HealthAppDbContext db) : EfRepository(db), ICustomerCreditRepository
{
    public async Task<decimal> GetBalanceAsync(Guid customerId)
    {
        return await db.CustomerCreditTransactions.Where(x => x.CustomerId == customerId).Select(x => x.Type == CreditTransactionType.Credit || x.Type == CreditTransactionType.Refund ? x.Amount : -x.Amount).SumAsync();
    }
    public async Task<IReadOnlyList<CustomerCreditTransaction>> GetTransactionsAsync(Guid customerId) => await db.CustomerCreditTransactions.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt).ToListAsync();
    public async Task AddAsync(CustomerCreditTransaction transaction) {
        db.CustomerCreditTransactions.Add(transaction);
        await SaveAsync();
    }
}

public sealed class OrderRepository(HealthAppDbContext db) : EfRepository(db), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid id) => await db.Orders.AsNoTracking().Where(x => x.CustomerId == id).OrderByDescending(x => x.DeliveryDate).ToListAsync();
    public async Task<IReadOnlyList<Order>> GetByOutletAsync(Guid id) => await db.Orders.AsNoTracking().Where(x => x.OutletId == id).OrderByDescending(x => x.DeliveryDate).ToListAsync();
    public Task<Order?> GetBySubscriptionAsync(Guid subscriptionId) => db.Orders.FirstOrDefaultAsync(x=>x.SubscriptionId==subscriptionId);
    public async Task AddAsync(Order order) {
        db.Orders.Add(order);
        await SaveAsync();
    }
    public async Task UpdateAsync(Order order) {
        db.Orders.Update(order);
        await SaveAsync();
    }
}

public sealed class DeliveryRepository(HealthAppDbContext db) : EfRepository(db), IDeliveryRepository
{
    public async Task<IReadOnlyList<Delivery>> GetByOutletAsync(Guid id) => await db.Deliveries.AsNoTracking().Where(x => x.OutletId == id).OrderBy(x => x.ScheduledDate).ToListAsync();
    public async Task AddAsync(Delivery delivery) {
        db.Deliveries.Add(delivery);
        await SaveAsync();
    }
    public Task<Delivery?> GetAsync(Guid id) => db.Deliveries.FirstOrDefaultAsync(x => x.Id == id);
    public async Task UpdateAsync(Delivery delivery) {
        db.Deliveries.Update(delivery);
        await SaveAsync();
    }
    public async Task<IReadOnlyList<Delivery>> GetBySubscriptionAsync(Guid subscriptionId) => await db.Deliveries.AsNoTracking().Where(x => x.SubscriptionId == subscriptionId).OrderBy(x => x.ScheduledDate).ThenBy(x => x.MealSlot).ToListAsync();
}

public sealed class DeliveryRouteRepository(HealthAppDbContext db) : EfRepository(db), IDeliveryRouteRepository
{
    public async Task<IReadOnlyList<DeliveryRoute>> GetByOutletAndDateAsync(Guid outletId, DateTime date, MealSlot mealSlot)
        => await db.DeliveryRoutes.AsNoTracking().Include(x => x.Stops).Where(x => x.OutletId == outletId && x.DeliveryDate >= date.Date && x.DeliveryDate < date.Date.AddDays(1) && x.MealSlot == mealSlot).OrderBy(x => x.DriverId).ToListAsync();

    public async Task DeleteByOutletAndDateAsync(Guid outletId, DateTime date, MealSlot mealSlot)
    {
        var routes = await db.DeliveryRoutes.Where(x => x.OutletId == outletId && x.DeliveryDate >= date.Date && x.DeliveryDate < date.Date.AddDays(1) && x.MealSlot == mealSlot).ToListAsync();
        if (routes.Count == 0) return;
        var routeIds = routes.Select(x => x.Id).ToList();
        // Clear route links with a bulk SQL UPDATE so the deliveries are not tracked.
        // PlanRoutesAsync reloads eligible deliveries as no-tracking and later updates those
        // instances. Keeping another tracked instance here causes EF identity-map conflicts.
        await db.Deliveries
            .Where(x => x.RouteId.HasValue && routeIds.Contains(x.RouteId.Value))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RouteId, (Guid?)null)
                .SetProperty(x => x.RouteStopId, (Guid?)null)
                .SetProperty(x => x.RouteSequence, (int?)null));

        db.DeliveryRoutes.RemoveRange(routes);
        await SaveAsync();
    }

    public async Task AddAsync(DeliveryRoute route)
    {
        db.DeliveryRoutes.Add(route);
        await SaveAsync();
    }
}

public sealed class CustomerProfileRepository(HealthAppDbContext db) : EfRepository(db), ICustomerProfileRepository
{
    public Task<CustomerProfile?> GetAsync(Guid customerId) => db.CustomerProfiles
    .FirstOrDefaultAsync(x => x.CustomerId == customerId);
    public async Task AddOrUpdateAsync(CustomerProfile profile)
    {
        var existing = await db.CustomerProfiles.FirstOrDefaultAsync(x => x.CustomerId == profile.CustomerId);
        if (existing is null) db.CustomerProfiles.Add(profile);
        else db.Entry(existing).CurrentValues.SetValues(profile);
        await SaveAsync();
    }
    public async Task ReplaceAllergiesAsync(Guid customerId, IReadOnlyCollection<Guid> allergenIds)
    {
        var existing = await db.CustomerAllergies.Where(x => x.CustomerId == customerId).ToListAsync();
        db.CustomerAllergies.RemoveRange(existing);
        db.CustomerAllergies.AddRange(allergenIds.Distinct().Select(allergenId => new CustomerAllergy {
            Id=Guid.NewGuid(), CustomerId=customerId, AllergenId=allergenId
        }));
        await SaveAsync();
    }
}

public sealed class IngredientRepository(HealthAppDbContext db) : EfRepository(db), IIngredientRepository
{
    public async Task<IReadOnlyList<Ingredient>> GetActiveAsync() => await db.Ingredients.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync();
    public async Task<IReadOnlyList<Ingredient>> GetByIdsAsync(IEnumerable<Guid> ids) => await db.Ingredients.Where(x=>ids.Contains(x.Id)&&x.IsActive).ToListAsync();
}

public sealed class AllergenRepository(HealthAppDbContext db) : EfRepository(db), IAllergenRepository
{
    public async Task<IReadOnlyList<Allergen>> GetActiveAsync() => await db.Allergens.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync();
    public async Task<IReadOnlyList<Allergen>> GetByIdsAsync(IEnumerable<Guid> ids) => await db.Allergens.Where(x=>ids.Contains(x.Id)&&x.IsActive).ToListAsync();
}

public sealed class CustomerAllergyRepository(HealthAppDbContext db) : EfRepository(db), ICustomerAllergyRepository
{
    public async Task<IReadOnlyList<CustomerAllergy>> GetByCustomerAsync(Guid customerId) => await db.CustomerAllergies.AsNoTracking().Include(x=>x.Allergen).Where(x=>x.CustomerId==customerId).ToListAsync();
    public async Task ReplaceAsync(Guid customerId, IReadOnlyCollection<Guid> allergenIds)
    {
        var old = await db.CustomerAllergies.Where(x=>x.CustomerId==customerId).ToListAsync();
        db.CustomerAllergies.RemoveRange(old);
        db.CustomerAllergies.AddRange(allergenIds.Distinct().Select(x=>new CustomerAllergy {
            Id=Guid.NewGuid(),CustomerId=customerId,AllergenId=x
        }));
        await SaveAsync();
    }
}

public sealed class CityAreaRepository(HealthAppDbContext db) : EfRepository(db), ICityAreaRepository
{
    public async Task<IReadOnlyList<CityArea>> GetActiveAsync(string? city = null)
    {
        var q = db.CityAreas.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(x => x.City == city);
        return await q.OrderBy(x => x.City).ThenBy(x => x.Name).ToListAsync();
    }
    public Task<CityArea?> GetAsync(Guid id) => db.CityAreas.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddAsync(CityArea area) {
        db.CityAreas.Add(area);
        await SaveAsync();
    }
}

public sealed class OutletDeliveryAreaRepository(HealthAppDbContext db) : EfRepository(db), IOutletDeliveryAreaRepository
{
    public async Task<IReadOnlyList<OutletDeliveryArea>> GetByOutletAsync(Guid outletId) => await db.OutletDeliveryAreas.AsNoTracking().Where(x => x.OutletId == outletId && x.IsActive).OrderBy(x => x.CityAreaId).ToListAsync();
    public async Task<IReadOnlyList<CityArea>> GetAreasForOutletAsync(Guid outletId) => await (from oa in db.OutletDeliveryAreas.AsNoTracking() join a in db.CityAreas.AsNoTracking() on oa.CityAreaId equals a.Id where oa.OutletId == outletId && oa.IsActive && a.IsActive orderby a.Name select a).ToListAsync();
    public async Task ReplaceAsync(Guid outletId, IEnumerable<OutletDeliveryArea> areas)
    {
        var existing = await db.OutletDeliveryAreas.Where(x => x.OutletId == outletId).ToListAsync();
        db.OutletDeliveryAreas.RemoveRange(existing);
        db.OutletDeliveryAreas.AddRange(areas);
        await SaveAsync();
    }
}

public sealed class DeliveryPricingRepository(HealthAppDbContext db) : EfRepository(db), IDeliveryPricingRepository
{
    public async Task<IReadOnlyList<DeliveryPricingRule>> GetByOutletAsync(Guid outletId) => await db.DeliveryPricingRules.AsNoTracking().Where(x => x.OutletId == outletId && x.IsActive).OrderBy(x => x.MaxDistanceKm).ToListAsync();
    public async Task AddAsync(DeliveryPricingRule rule) {
        db.DeliveryPricingRules.Add(rule);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid id, Guid outletId) {
        var x = await db.DeliveryPricingRules.FirstOrDefaultAsync(x => x.Id == id && x.OutletId == outletId);
        if (x is not null) {
            db.DeliveryPricingRules.Remove(x);
            await SaveAsync();
        }
    }
}

public sealed class CustomerAddressRepository(HealthAppDbContext db) : EfRepository(db), ICustomerAddressRepository
{
    public async Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(Guid customerId) => await db.CustomerAddresses.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Label).ToListAsync();
    public Task<CustomerAddress?> GetAsync(Guid customerId, Guid id) => db.CustomerAddresses.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.Id == id);
    public async Task AddAsync(CustomerAddress address) {
        if (address.IsDefault) await ClearDefaults(address.CustomerId, null);
        db.CustomerAddresses.Add(address);
        await SaveAsync();
    }
    public async Task UpdateAsync(CustomerAddress address) {
        if (address.IsDefault) await ClearDefaults(address.CustomerId, address.Id);
        db.CustomerAddresses.Update(address);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid customerId, Guid id) {
        var x = await GetAsync(customerId,id);
        if (x is not null) {
            db.CustomerAddresses.Remove(x);
            await SaveAsync();
        }
    }
    private async Task ClearDefaults(Guid customerId, Guid? except) {
        var rows = await db.CustomerAddresses.Where(x => x.CustomerId == customerId && x.IsDefault && (!except.HasValue || x.Id != except.Value)).ToListAsync();
        foreach (var x in rows) x.IsDefault=false;
    }
}

public sealed class SubscriptionDiscountTierRepository(HealthAppDbContext db) : EfRepository(db), ISubscriptionDiscountTierRepository
{
    public async Task<IReadOnlyList<SubscriptionDiscountTier>> GetByOutletAsync(Guid outletId) => await db.SubscriptionDiscountTiers.AsNoTracking().Where(x => x.OutletId == outletId && x.IsActive).OrderBy(x => x.MinMeals).ToListAsync();
    public async Task AddAsync(SubscriptionDiscountTier tier) {
        db.SubscriptionDiscountTiers.Add(tier);
        await SaveAsync();
    }
    public async Task UpdateAsync(SubscriptionDiscountTier tier) {
        db.SubscriptionDiscountTiers.Update(tier);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid outletId, Guid id) {
        var x = await db.SubscriptionDiscountTiers.FirstOrDefaultAsync(x=>x.Id==id&&x.OutletId==outletId);
        if(x is not null) {
            db.SubscriptionDiscountTiers.Remove(x);
            await SaveAsync();
        }
    }
}

public sealed class MealSelectionHistoryRepository(HealthAppDbContext db) : EfRepository(db), IMealSelectionHistoryRepository
{
    public async Task AddAsync(MealSelectionHistory history) {
        db.MealSelectionHistories.Add(history);
        await SaveAsync();
    }
    public async Task<IReadOnlyList<MealSelectionHistory>> GetBySelectionAsync(Guid selectionId) => await db.MealSelectionHistories.AsNoTracking().Where(x=>x.MealSelectionId==selectionId).OrderByDescending(x=>x.OccurredAtUtc).ToListAsync();
}

public sealed class PaymentTransactionRepository(HealthAppDbContext db) : EfRepository(db), IPaymentTransactionRepository
{
    public Task<PaymentTransaction?> GetAsync(Guid id) => db.PaymentTransactions.FirstOrDefaultAsync(x=>x.Id==id);
    public Task<PaymentTransaction?> GetLatestBySubscriptionAsync(Guid subscriptionId) => db.PaymentTransactions.AsNoTracking().Where(x=>x.SubscriptionId==subscriptionId).OrderByDescending(x=>x.CreatedAtUtc).FirstOrDefaultAsync();
    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string key) => db.PaymentTransactions.FirstOrDefaultAsync(x=>x.IdempotencyKey==key);
    public async Task AddAsync(PaymentTransaction payment) {
        db.PaymentTransactions.Add(payment);
        await SaveAsync();
    }
    public async Task UpdateAsync(PaymentTransaction payment) {
        db.PaymentTransactions.Update(payment);
        await SaveAsync();
    }
}

public sealed class DiscountCodeRepository(HealthAppDbContext db) : EfRepository(db), IDiscountCodeRepository
{
    public Task<DiscountCode?> GetAsync(Guid? outletId, string code) => db.DiscountCodes.FirstOrDefaultAsync(x => x.OutletId == outletId && x.Code == code.Trim().ToUpperInvariant() && x.IsActive);
    public async Task<IReadOnlyList<DiscountCode>> GetByOutletAsync(Guid outletId) => await db.DiscountCodes.AsNoTracking().Where(x=>x.OutletId==outletId).OrderBy(x=>x.Code).ToListAsync();
    public async Task AddAsync(DiscountCode code) {
        db.DiscountCodes.Add(code);
        await SaveAsync();
    }
    public async Task UpdateAsync(DiscountCode code) {
        db.DiscountCodes.Update(code);
        await SaveAsync();
    }
}

public sealed class OrderFinancialRepository(HealthAppDbContext db) : EfRepository(db), IOrderFinancialRepository
{
    public async Task AddAsync(OrderFinancialBreakdown breakdown) {
        db.OrderFinancialBreakdowns.Add(breakdown);
        await SaveAsync();
    }
    public Task<OrderFinancialBreakdown?> GetByOrderAsync(Guid orderId) => db.OrderFinancialBreakdowns.FirstOrDefaultAsync(x=>x.OrderId==orderId);
}
