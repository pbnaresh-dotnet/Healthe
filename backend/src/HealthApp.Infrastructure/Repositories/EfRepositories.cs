using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Infrastructure.Data;
using HealthApp.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
namespace HealthApp.Infrastructure.Repositories;

public abstract class EfRepository(HealthAppDbContext Db)
{
    protected HealthAppDbContext Context => Db;
    protected Task<int> SaveAsync(CancellationToken ct = default) => Db.SaveChangesAsync(ct);
}

public sealed class UserRepository(HealthAppDbContext db) : EfRepository(db), IUserRepository
{
    public Task<User?> FindByEmailAsync(string email) => Context.Users.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLower());
    public Task<User?> FindByEmailAsync(string email, Guid outletId) => Context.Users.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLower() && x.OutletId == outletId);
    public Task<User?> FindByMobileAsync(string mobileNumber, Guid? outletId = null) => Context.Users.FirstOrDefaultAsync(x => x.MobileNumber == mobileNumber.Trim() && x.OutletId == outletId);
    public async Task<IReadOnlyList<User>> FindTenantUsersByEmailAsync(string email) => await Context.Users.AsNoTracking().Where(x => x.Email == email.Trim().ToLower() && x.OutletId != null).OrderBy(x => x.OutletId).ThenBy(x => x.Role).ToListAsync();
    public Task<User?> FindByIdAsync(Guid id) => Context.Users.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddAsync(User user) {
        Context.Users.Add(user);
        await SaveAsync();
    }
    public async Task UpdateAsync(User user) {
        Context.Users.Update(user);
        await SaveAsync();
    }
    public async Task<IReadOnlyList<User>> GetAllAsync() => await Context.Users.AsNoTracking().OrderBy(x => x.Email).ToListAsync();
    public Task<int> CountAsync(UserRole? role = null)
    {
        var query = Context.Users.AsNoTracking();
        if (role.HasValue) query = query.Where(x => x.Role == role.Value);
        return query.CountAsync();
    }
    public async Task<PageResult<User>> GetOutletCustomersPageAsync(Guid outletId, string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Context.Users.AsNoTracking()
            .Where(x => x.OutletId == outletId && x.Role == UserRole.Customer);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.FirstName.Contains(term) || x.LastName.Contains(term) ||
                x.Email.Contains(term) || (x.MobileNumber != null && x.MobileNumber.Contains(term)));
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PageResult<User>(items, total, page, pageSize);
    }
}

public sealed class OutletRepository(HealthAppDbContext db) : EfRepository(db), IOutletRepository
{
    public async Task<IReadOnlyList<Outlet>> GetAllAsync() => await Context.Outlets.AsNoTracking().Include(x => x.Branding).OrderBy(x => x.Name).ToListAsync();
    public Task<int> CountAsync(OutletStatus? status = null)
    {
        var query = Context.Outlets.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return query.CountAsync();
    }

    public async Task<PageResult<Outlet>> GetPageAsync(string? search, string? status, string? city, int page, int pageSize)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Outlet> query = Context.Outlets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Slug.Contains(term) ||
                x.Subdomain.Contains(term) || x.City.Contains(term) || x.State.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OutletStatus>(status.Trim(), true, out var parsedStatus))
            query = query.Where(x => x.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityTerm = city.Trim();
            query = query.Where(x => x.City == cityTerm);
        }

        var total = await query.CountAsync();
        var items = await query.Include(x => x.Branding)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return new PageResult<Outlet>(items, total, page, pageSize);
    }
    public Task<Outlet?> GetByIdAsync(Guid id) => Context.Outlets.Include(x => x.Branding).FirstOrDefaultAsync(x => x.Id == id);
    public Task<Outlet?> GetBySlugAsync(string slug) => Context.Outlets.Include(x => x.Branding).FirstOrDefaultAsync(x => x.Slug == slug.Trim().ToLower());
    public Task<Outlet?> GetBySubdomainAsync(string subdomain) => Context.Outlets.Include(x => x.Branding).FirstOrDefaultAsync(x => x.Subdomain == subdomain.Trim().ToLower());
    public async Task AddAsync(Outlet outlet) {
        Context.Outlets.Add(outlet);
        await SaveAsync();
    }
    public async Task UpdateAsync(Outlet outlet) {
        Context.Outlets.Update(outlet);
        await SaveAsync();
    }
}

public sealed class OutletBrandingRepository(HealthAppDbContext db) : EfRepository(db), IOutletBrandingRepository
{
    public Task<OutletBranding?> GetByOutletAsync(Guid outletId) => Context.OutletBrandings.FirstOrDefaultAsync(x => x.OutletId == outletId);
    public async Task AddAsync(OutletBranding branding) { Context.OutletBrandings.Add(branding); await SaveAsync(); }
    public async Task UpdateAsync(OutletBranding branding) { Context.OutletBrandings.Update(branding); await SaveAsync(); }
}

public sealed class OutletDomainRepository(HealthAppDbContext db) : EfRepository(db), IOutletDomainRepository
{
    public Task<OutletDomain?> GetAsync(Guid id) =>
        Context.OutletDomains.Include(x => x.Outlet).FirstOrDefaultAsync(x => x.Id == id);

    public Task<OutletDomain?> GetActiveByHostnameAsync(string hostname)
    {
        var value = hostname.Trim().TrimEnd('.').ToLowerInvariant();
        return Context.OutletDomains.AsNoTracking()
            .Include(x => x.Outlet)
             .ThenInclude(x => x!.Branding)
            .FirstOrDefaultAsync(x => x.Hostname == value && x.Status == OutletDomainStatus.Active);
    }

    public Task<OutletDomain?> GetByHostnameAsync(string hostname)
    {
        var value = hostname.Trim().TrimEnd('.').ToLowerInvariant();
        return Context.OutletDomains.Include(x => x.Outlet).FirstOrDefaultAsync(x => x.Hostname == value);
    }

    public async Task<IReadOnlyList<OutletDomain>> GetByOutletAsync(Guid outletId) =>
        await Context.OutletDomains.AsNoTracking()
            .Where(x => x.OutletId == outletId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Hostname)
            .ToListAsync();

    public async Task<IReadOnlyList<OutletDomain>> GetAllAsync() =>
        await Context.OutletDomains.AsNoTracking()
            .Include(x => x.Outlet)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

    public async Task AddAsync(OutletDomain domain) { Context.OutletDomains.Add(domain); await SaveAsync(); }
    public async Task UpdateAsync(OutletDomain domain) { Context.OutletDomains.Update(domain); await SaveAsync(); }
}

public sealed class SaaSPlanRepository(HealthAppDbContext db) : EfRepository(db), ISaaSPlanRepository
{
    public async Task<IReadOnlyList<SaaSPlan>> GetActiveAsync() => await Context.SaaSPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.MonthlyFee).ToListAsync();
    public Task<SaaSPlan?> GetAsync(Guid id) => Context.SaaSPlans.FirstOrDefaultAsync(x => x.Id == id);
}

public sealed class OutletSubscriptionRepository(HealthAppDbContext db) : EfRepository(db), IOutletSubscriptionRepository
{
    public Task<OutletSubscription?> GetByOutletAsync(Guid outletId) => Context.OutletSubscriptions.Include(x => x.Trial).FirstOrDefaultAsync(x => x.OutletId == outletId && x.Status == "Active" && (x.Trial == null || (x.Trial.Status == TrialStatus.Active && x.Trial.EndsAtUtc > DateTime.UtcNow)));
    public Task<OutletSubscription?> GetAnyByOutletAsync(Guid outletId) => Context.OutletSubscriptions.FirstOrDefaultAsync(x => x.OutletId == outletId);
    public async Task<IReadOnlySet<Guid>> GetActiveOutletIdsAsync() =>
        (await Context.OutletSubscriptions.AsNoTracking()
            .Where(x => x.Status == "Active")
            .Select(x => x.OutletId)
            .ToListAsync())
            .ToHashSet();
    public async Task AddAsync(OutletSubscription subscription) {
        Context.OutletSubscriptions.Add(subscription);
        await SaveAsync();
    }
    public async Task UpdateAsync(OutletSubscription subscription) {
        Context.OutletSubscriptions.Update(subscription);
        await SaveAsync();
    }
}

public sealed class TrialRepository(HealthAppDbContext db) : EfRepository(db), ITrialRepository
{
    public Task<Trial?> GetByOutletAsync(Guid outletId) => Context.Trials.FirstOrDefaultAsync(x => x.OutletId == outletId);
    public async Task AddAsync(Trial trial) { Context.Trials.Add(trial); await SaveAsync(); }
    public async Task UpdateAsync(Trial trial) { Context.Trials.Update(trial); await SaveAsync(); }
}

public sealed class PlatformTransactionRepository(HealthAppDbContext db) : EfRepository(db), IPlatformTransactionRepository
{
    public async Task AddAsync(PlatformTransaction transaction) {
        Context.PlatformTransactions.Add(transaction);
        await SaveAsync();
    }
    public async Task UpdateAsync(PlatformTransaction transaction) {
        Context.PlatformTransactions.Update(transaction);
        await SaveAsync();
    }
    public async Task<PlatformRevenueDto> GetRevenueSummaryAsync()
    {
        var summary = await Context.PlatformTransactions.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new PlatformRevenueDto(
                g.Where(x => x.Type == "OutletSubscription").Sum(x => x.GrossAmount),
                g.Where(x => x.Type == "CustomerSubscription").Sum(x => x.PlatformFee),
                g.Where(x => x.Type == "OutletSubscription").Sum(x => x.GrossAmount)
                    + g.Where(x => x.Type == "CustomerSubscription").Sum(x => x.PlatformFee)
                    + g.Where(x => x.Type == "LateSkipFee").Sum(x => x.GrossAmount)
                    + g.Where(x => x.Type == "OutletCommission").Sum(x => x.GrossAmount),
                g.Where(x => x.Type == "LateSkipFee").Sum(x => x.GrossAmount),
                g.Where(x => x.Type == "CustomerSubscription").Sum(x => x.PlatformFee),
                g.Where(x => x.Type == "OutletCommission").Sum(x => x.GrossAmount)))
            .FirstOrDefaultAsync();
        return summary ?? new PlatformRevenueDto(0m, 0m, 0m, 0m, 0m, 0m);
    }
    public async Task<(decimal GrossAmount, decimal PlatformFee)> GetOutletSummaryAsync(Guid outletId, DateTime fromUtc)
    {
        var summary = await Context.PlatformTransactions.AsNoTracking()
            .Where(x => x.OutletId == outletId && x.CreatedAt >= fromUtc)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                GrossAmount = g.Sum(x => x.GrossAmount),
                PlatformFee = g.Sum(x => x.PlatformFee)
            })
            .FirstOrDefaultAsync();
        return summary is null ? (0m, 0m) : (summary.GrossAmount, summary.PlatformFee);
    }

    public Task<bool> ExistsForSubscriptionAsync(Guid subscriptionId, string type) =>
        Context.PlatformTransactions.AsNoTracking().AnyAsync(x => x.SubscriptionId == subscriptionId && x.Type == type);

    public async Task<IReadOnlyList<PlatformTransaction>> GetPendingLateSkipFeesAsync(Guid customerId, Guid outletId) =>
        await Context.PlatformTransactions
            .Where(x => x.CustomerId == customerId && x.OutletId == outletId && x.Type == "LateSkipFee" && x.Status == "Pending")
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync();

    public Task<PlatformTransaction?> GetRecoveryTransactionAsync(Guid subscriptionId) =>
        Context.PlatformTransactions.FirstOrDefaultAsync(x => x.SubscriptionId == subscriptionId && x.Type == "LateSkipFeeRecovery" && x.Status != "Paid");

    public Task<bool> ExistsByReferenceAsync(string referenceId) => Context.PlatformTransactions.AnyAsync(x => x.ReferenceId == referenceId);
}

public sealed class MealPlanRepository(HealthAppDbContext db) : EfRepository(db), IMealPlanRepository
{
    public async Task<IReadOnlyList<MealPlan>> GetByOutletAsync(Guid outletId) => await Context.MealPlans.AsNoTracking().Where(x => x.OutletId == outletId).OrderBy(x => x.Name).ToListAsync();
    public Task<MealPlan?> GetAsync(Guid id) => Context.MealPlans.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddAsync(MealPlan plan) {
        Context.MealPlans.Add(plan);
        await SaveAsync();
    }
}

public sealed class RecipeRepository(HealthAppDbContext db) : EfRepository(db), IRecipeRepository
{
    private IQueryable<Recipe> Details(IQueryable<Recipe> query) => query
    .Include(x => x.RecipeIngredients).ThenInclude(x => x.Ingredient).ThenInclude(x => x.Allergens).ThenInclude(x => x.Allergen)
    .Include(x => x.RecipeAllergens).ThenInclude(x => x.Allergen);
    public async Task<IReadOnlyList<Recipe>> GetByOutletAsync(Guid outletId) => await Details(Context.Recipes.AsNoTracking().Where(x => x.OutletId == outletId)).OrderBy(x => x.Name).ToListAsync();
    public async Task<IReadOnlyList<Recipe>> GetByOutletAndCategoryAsync(Guid outletId, string? category)
    {
        var q = Details(Context.Recipes.AsNoTracking().Where(x => x.OutletId == outletId));
        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<RecipeCategory>(category, true, out var parsed)) q = q.Where(x => x.Category == parsed);
        return await q.OrderBy(x => x.Name).ToListAsync();
    }
    public async Task<PageResult<Recipe>> GetByOutletPageAsync(Guid outletId, string? category, string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = Context.Recipes.AsNoTracking().Where(x => x.OutletId == outletId);
        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<RecipeCategory>(category, true, out var parsed))
            q = q.Where(x => x.Category == parsed);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.Name.Contains(term) || (x.Description != null && x.Description.Contains(term)));
        }
        var total = await q.CountAsync();
        var items = await Details(q).OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PageResult<Recipe>(items, total, page, pageSize);
    }
    public async Task<IReadOnlyList<Recipe>> GetByIdsAsync(IEnumerable<Guid> ids) => await Details(Context.Recipes.AsNoTracking().Where(x => ids.Contains(x.Id))).ToListAsync();
    public async Task<IReadOnlyList<Recipe>> GetByIdsForOutletAsync(IEnumerable<Guid> ids, Guid outletId) => await Details(Context.Recipes.AsNoTracking().Where(x => ids.Contains(x.Id) && x.OutletId == outletId)).ToListAsync();
    public Task<Recipe?> GetAsync(Guid id) => Details(Context.Recipes.Where(x => x.Id == id)).FirstOrDefaultAsync();
    public Task<Recipe?> GetForOutletAsync(Guid id, Guid outletId) => Details(Context.Recipes.Where(x => x.Id == id && x.OutletId == outletId)).FirstOrDefaultAsync();
    public async Task AddAsync(Recipe recipe) {
        Context.Recipes.Add(recipe);
        await SaveAsync();
    }
    public async Task UpdateAsync(Recipe recipe) {
        Context.Recipes.Update(recipe);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid id)
    {
        var recipe = await Context.Recipes.FirstOrDefaultAsync(x => x.Id == id);
        if (recipe is null) return;
        Context.Recipes.Remove(recipe);
        await SaveAsync();
    }

    public async Task<bool> DeleteAsync(Guid id, Guid outletId)
    {
        var recipe = await Context.Recipes.FirstOrDefaultAsync(x => x.Id == id && x.OutletId == outletId);
        if (recipe is null) return false;
        Context.Recipes.Remove(recipe);
        await SaveAsync();
        return true;
    }
}

public sealed class CustomerLikedMealRepository(HealthAppDbContext db) : EfRepository(db), ICustomerLikedMealRepository
{
    public async Task<IReadOnlyList<CustomerLikedMeal>> GetByCustomerAsync(Guid customerId) =>
        await Context.CustomerLikedMeals.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

    public Task<bool> ExistsAsync(Guid customerId, Guid recipeId) =>
        Context.CustomerLikedMeals.AnyAsync(x => x.CustomerId == customerId && x.RecipeId == recipeId);

    public async Task AddAsync(CustomerLikedMeal meal)
    {
        Context.CustomerLikedMeals.Add(meal);
        await SaveAsync();
    }

    public async Task RemoveAsync(Guid customerId, Guid recipeId)
    {
        var meal = await Context.CustomerLikedMeals.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.RecipeId == recipeId);
        if (meal is not null)
        {
            Context.CustomerLikedMeals.Remove(meal);
            await SaveAsync();
        }
    }
}

public sealed class OutletMenuRepository(HealthAppDbContext db) : EfRepository(db), IOutletMenuRepository
{
    public async Task<IReadOnlyList<OutletMenuItem>> GetByOutletAsync(Guid outletId) => await Context.OutletMenuItems.AsNoTracking().Where(x => x.OutletId == outletId).OrderBy(x => x.DayOfWeek).ThenBy(x => x.MealSlot).ThenBy(x => x.DisplayOrder).ToListAsync();
    public async Task<IReadOnlyList<OutletMenuItem>> GetByOutletDayAsync(Guid outletId, DayOfWeek day) => await Context.OutletMenuItems.AsNoTracking().Where(x => x.OutletId == outletId && x.DayOfWeek == day && x.IsAvailable).OrderBy(x => x.MealSlot).ThenBy(x => x.DisplayOrder).ToListAsync();
    public async Task AddAsync(OutletMenuItem item) {
        Context.OutletMenuItems.Add(item);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid id) {
        var item = await Context.OutletMenuItems.FindAsync(id);
        if (item is not null) {
            Context.OutletMenuItems.Remove(item);
            await SaveAsync();
        }
    }
    public async Task ReplaceAsync(Guid outletId, IEnumerable<OutletMenuItem> items)
    {
        var existing = await Context.OutletMenuItems.Where(x => x.OutletId == outletId).ToListAsync();
        Context.OutletMenuItems.RemoveRange(existing);
        Context.OutletMenuItems.AddRange(items);
        await SaveAsync();
    }
}

public sealed class SubscriptionRepository(HealthAppDbContext db) : EfRepository(db), ISubscriptionRepository
{
    public async Task<IReadOnlyList<Subscription>> GetByCustomerAsync(Guid id) =>
        await Context.Subscriptions.AsNoTracking()
            .Where(x => x.CustomerId == id && Context.Users.Any(u => u.Id == id && u.OutletId == x.OutletId))
            .OrderByDescending(x => x.StartDate)
            .ToListAsync();
    public async Task<IReadOnlyList<Subscription>> GetByOutletAsync(Guid id) => await Context.Subscriptions.AsNoTracking().Where(x => x.OutletId == id).OrderByDescending(x => x.StartDate).ToListAsync();
    public async Task<PageResult<Subscription>> GetByOutletPageAsync(Guid id, string? search, string? status, int page, int pageSize)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = Context.Subscriptions.AsNoTracking().Where(x => x.OutletId == id);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubscriptionStatus>(status, true, out var parsedStatus))
            q = q.Where(x => x.Status == parsedStatus);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.PlanName.Contains(term) || x.CustomerId.ToString().Contains(term));
        }
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.StartDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PageResult<Subscription>(items, total, page, pageSize);
    }
    public Task<Subscription?> GetAsync(Guid id) => Context.Subscriptions.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddAsync(Subscription s) {
        Context.Subscriptions.Add(s);
        await SaveAsync();
    }
    public async Task UpdateAsync(Subscription s) {
        Context.Subscriptions.Update(s);
        await SaveAsync();
    }
}

public sealed class SubscriptionMealSelectionRepository(HealthAppDbContext db) : EfRepository(db), ISubscriptionMealSelectionRepository
{
    public async Task<IReadOnlyList<SubscriptionMealSelection>> GetBySubscriptionAsync(Guid id) => await Context.SubscriptionMealSelections.AsNoTracking().Where(x => x.SubscriptionId == id).OrderBy(x => x.MealDate).ThenBy(x => x.MealSlot).ToListAsync();
    public async Task<IReadOnlyList<SubscriptionMealSelection>> GetBySubscriptionAndDateRangeAsync(Guid id, DateTime from, DateTime to) => await Context.SubscriptionMealSelections.AsNoTracking().Where(x => x.SubscriptionId == id && x.MealDate >= from && x.MealDate < to).OrderBy(x => x.MealDate).ThenBy(x => x.MealSlot).ToListAsync();
    public async Task<IReadOnlyList<SubscriptionMealSelection>> GetByOutletAndDateRangeAsync(Guid outletId, DateTime from, DateTime to) =>
        await Context.SubscriptionMealSelections.AsNoTracking()
            .Where(x => x.MealDate >= from && x.MealDate < to && Context.Subscriptions.Any(s => s.Id == x.SubscriptionId && s.OutletId == outletId))
            .OrderBy(x => x.SubscriptionId)
            .ThenBy(x => x.MealDate)
            .ThenBy(x => x.MealSlot)
            .ToListAsync();
    public Task<SubscriptionMealSelection?> GetAsync(Guid id) => Context.SubscriptionMealSelections.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddRangeAsync(IEnumerable<SubscriptionMealSelection> selections) {
        Context.SubscriptionMealSelections.AddRange(selections);
        await SaveAsync();
    }
    public async Task UpdateAsync(SubscriptionMealSelection selection) {
        Context.SubscriptionMealSelections.Update(selection);
        await SaveAsync();
    }
    public async Task DeleteBySubscriptionAndDateRangeAsync(Guid id, DateTime from, DateTime to) {
        var rows = await Context.SubscriptionMealSelections.Where(x => x.SubscriptionId == id && x.MealDate >= from && x.MealDate < to).ToListAsync();
        Context.SubscriptionMealSelections.RemoveRange(rows);
        await SaveAsync();
    }
}

public sealed class CustomerCreditRepository(HealthAppDbContext db) : EfRepository(db), ICustomerCreditRepository
{
    public async Task<decimal> GetBalanceAsync(Guid customerId)
    {
        var customer = await Context.Users.AsNoTracking().Where(x => x.Id == customerId).Select(x => new { x.Role, x.OutletId }).FirstOrDefaultAsync();
        if (customer is null || customer.Role != UserRole.Customer || !customer.OutletId.HasValue)
            return 0m;

        return await Context.CustomerCreditTransactions
            .Where(x => x.CustomerId == customerId)
            .Select(x => x.Type == CreditTransactionType.Credit || x.Type == CreditTransactionType.Refund ? x.Amount : -x.Amount)
            .SumAsync();
    }
    public async Task<decimal> GetOutstandingLateSkipAmountAsync(Guid customerId)
    {
        // Wallet-covered late skips are marked Paid by the domain event handler.
        // Only pending late-skip receivables are carried into a future package.
        return await Context.PlatformTransactions.AsNoTracking()
            .Where(x => x.CustomerId == customerId &&
                        x.Type == "LateSkipFee" &&
                        x.Status == "Pending")
            .SumAsync(x => x.GrossAmount);
    }
    public async Task<IReadOnlyList<CustomerCreditTransaction>> GetTransactionsAsync(Guid customerId)
    {
        var customer = await Context.Users.AsNoTracking().Where(x => x.Id == customerId).Select(x => new { x.Role, x.OutletId }).FirstOrDefaultAsync();
        if (customer is null || customer.Role != UserRole.Customer || !customer.OutletId.HasValue)
            return [];

        return await Context.CustomerCreditTransactions.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
    public async Task AddAsync(CustomerCreditTransaction transaction) {
        Context.CustomerCreditTransactions.Add(transaction);
        await SaveAsync();
    }
}

public sealed class OrderRepository(HealthAppDbContext db) : EfRepository(db), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid id) =>
        await Context.Orders.AsNoTracking()
            .Where(x => x.CustomerId == id && Context.Users.Any(u => u.Id == id && u.OutletId == x.OutletId))
            .OrderByDescending(x => x.DeliveryDate)
            .ToListAsync();
    public async Task<IReadOnlyList<Order>> GetByOutletAsync(Guid id) => await Context.Orders.AsNoTracking().Where(x => x.OutletId == id).OrderByDescending(x => x.DeliveryDate).ToListAsync();
    public async Task<PageResult<Order>> GetByOutletPageAsync(Guid id, string? search, string? status, int page, int pageSize)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = Context.Orders.AsNoTracking().Where(x => x.OutletId == id);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
            q = q.Where(x => x.Status == parsedStatus);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.Address.Contains(term) || x.CustomerId.ToString().Contains(term));
        }
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.DeliveryDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PageResult<Order>(items, total, page, pageSize);
    }
    public Task<Order?> GetBySubscriptionAsync(Guid subscriptionId) => Context.Orders.FirstOrDefaultAsync(x=>x.SubscriptionId==subscriptionId);
    public async Task AddAsync(Order order) {
        Context.Orders.Add(order);
        await SaveAsync();
    }
    public async Task UpdateAsync(Order order) {
        Context.Orders.Update(order);
        await SaveAsync();
    }
}

public sealed class DeliveryRepository(HealthAppDbContext db) : EfRepository(db), IDeliveryRepository
{
    public async Task<IReadOnlyList<Delivery>> GetByOutletAsync(Guid id) => await Context.Deliveries.AsNoTracking().Where(x => x.OutletId == id).OrderBy(x => x.ScheduledDate).ToListAsync();
    public async Task<PageResult<Delivery>> GetByOutletPageAsync(Guid id, string? search, string? status, DateTime? date, int page, int pageSize)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = Context.Deliveries.AsNoTracking().Where(x => x.OutletId == id);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DeliveryStatus>(status, true, out var parsedStatus))
            q = q.Where(x => x.Status == parsedStatus);
        if (date.HasValue) { var day = date.Value.Date; q = q.Where(x => x.ScheduledDate >= day && x.ScheduledDate < day.AddDays(1)); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.CustomerName.Contains(term) || x.Address.Contains(term));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(x => x.ScheduledDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PageResult<Delivery>(items, total, page, pageSize);
    }
    public async Task<IReadOnlyList<Delivery>> GetByOutletAndDateRangeAsync(Guid outletId, DateTime from, DateTime to) =>
        await Context.Deliveries.AsNoTracking()
            .Where(x => x.OutletId == outletId && x.ScheduledDate >= from && x.ScheduledDate < to)
            .OrderBy(x => x.ScheduledDate)
            .ThenBy(x => x.MealSlot)
            .ToListAsync();
    public async Task AddAsync(Delivery delivery) {
        Context.Deliveries.Add(delivery);
        await SaveAsync();
    }
    public Task<Delivery?> GetAsync(Guid id) => Context.Deliveries.FirstOrDefaultAsync(x => x.Id == id);
    public async Task UpdateAsync(Delivery delivery) {
        Context.Deliveries.Update(delivery);
        await SaveAsync();
    }
    public async Task<IReadOnlyList<Delivery>> GetBySubscriptionAsync(Guid subscriptionId) => await Context.Deliveries.AsNoTracking().Where(x => x.SubscriptionId == subscriptionId).OrderBy(x => x.ScheduledDate).ThenBy(x => x.MealSlot).ToListAsync();
}

public sealed class DeliveryRouteRepository(HealthAppDbContext db) : EfRepository(db), IDeliveryRouteRepository
{
    public async Task<IReadOnlyList<DeliveryRoute>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList=ids.Distinct().ToList();
        if(idList.Count==0)return [];
        return await Context.DeliveryRoutes.AsNoTracking().Include(x=>x.Stops).Where(x=>idList.Contains(x.Id)).ToListAsync();
    }

    public Task<DeliveryRoute?> GetAsync(Guid id)
        => Context.DeliveryRoutes.Include(x=>x.Stops).FirstOrDefaultAsync(x=>x.Id==id);

    public async Task<IReadOnlyList<DeliveryRoute>> GetByOutletAndDateAsync(Guid outletId, DateTime date, MealSlot mealSlot)
        => await Context.DeliveryRoutes.AsNoTracking().Include(x => x.Stops).Where(x => x.OutletId == outletId && x.DeliveryDate >= date.Date && x.DeliveryDate < date.Date.AddDays(1) && x.MealSlot == mealSlot).OrderBy(x => x.DriverId).ToListAsync();

    public async Task DeleteByOutletAndDateAsync(Guid outletId, DateTime date, MealSlot mealSlot)
    {
        var routes = await Context.DeliveryRoutes.Where(x => x.OutletId == outletId && x.DeliveryDate >= date.Date && x.DeliveryDate < date.Date.AddDays(1) && x.MealSlot == mealSlot).ToListAsync();
        if (routes.Count == 0) return;
        var routeIds = routes.Select(x => x.Id).ToList();
        // Clear route links with a bulk SQL UPDATE so the deliveries are not tracked.
        // PlanRoutesAsync reloads eligible deliveries as no-tracking and later updates those
        // instances. Keeping another tracked instance here causes EF identity-map conflicts.
        await Context.Deliveries
            .Where(x => x.RouteId.HasValue && routeIds.Contains(x.RouteId.Value))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RouteId, (Guid?)null)
                .SetProperty(x => x.RouteStopId, (Guid?)null)
                .SetProperty(x => x.RouteSequence, (int?)null));

        Context.DeliveryRoutes.RemoveRange(routes);
        await SaveAsync();
    }

    public async Task AddAsync(DeliveryRoute route)
    {
        Context.DeliveryRoutes.Add(route);
        await SaveAsync();
    }

    public async Task UpdateAsync(DeliveryRoute route)
    {
        Context.DeliveryRoutes.Update(route);
        await SaveAsync();
    }
}

public sealed class CustomerProfileRepository(HealthAppDbContext db) : EfRepository(db), ICustomerProfileRepository
{
    public Task<CustomerProfile?> GetAsync(Guid customerId) => Context.CustomerProfiles
    .FirstOrDefaultAsync(x => x.CustomerId == customerId);
    public async Task AddOrUpdateAsync(CustomerProfile profile)
    {
        var existing = await Context.CustomerProfiles.FirstOrDefaultAsync(x => x.CustomerId == profile.CustomerId);
        if (existing is null) Context.CustomerProfiles.Add(profile);
        else Context.Entry(existing).CurrentValues.SetValues(profile);
        await SaveAsync();
    }
    public async Task ReplaceAllergiesAsync(Guid customerId, IReadOnlyCollection<Guid> allergenIds)
    {
        var existing = await Context.CustomerAllergies.Where(x => x.CustomerId == customerId).ToListAsync();
        Context.CustomerAllergies.RemoveRange(existing);
        Context.CustomerAllergies.AddRange(allergenIds.Distinct().Select(allergenId => new CustomerAllergy {
            Id=Guid.NewGuid(), CustomerId=customerId, AllergenId=allergenId
        }));
        await SaveAsync();
    }
}

public sealed class IngredientRepository(HealthAppDbContext db) : EfRepository(db), IIngredientRepository
{
    public async Task<IReadOnlyList<Ingredient>> GetActiveAsync() => await Context.Ingredients.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync();
    public async Task<IReadOnlyList<Ingredient>> GetByIdsAsync(IEnumerable<Guid> ids) => await Context.Ingredients.Where(x=>ids.Contains(x.Id)&&x.IsActive).ToListAsync();
}

public sealed class AllergenRepository(HealthAppDbContext db) : EfRepository(db), IAllergenRepository
{
    public async Task<IReadOnlyList<Allergen>> GetActiveAsync() => await Context.Allergens.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync();
    public async Task<IReadOnlyList<Allergen>> GetByIdsAsync(IEnumerable<Guid> ids) => await Context.Allergens.Where(x=>ids.Contains(x.Id)&&x.IsActive).ToListAsync();
}

public sealed class CustomerAllergyRepository(HealthAppDbContext db) : EfRepository(db), ICustomerAllergyRepository
{
    public async Task<IReadOnlyList<CustomerAllergy>> GetByCustomerAsync(Guid customerId) => await Context.CustomerAllergies.AsNoTracking().Include(x=>x.Allergen).Where(x=>x.CustomerId==customerId).ToListAsync();
    public async Task ReplaceAsync(Guid customerId, IReadOnlyCollection<Guid> allergenIds)
    {
        var old = await Context.CustomerAllergies.Where(x=>x.CustomerId==customerId).ToListAsync();
        Context.CustomerAllergies.RemoveRange(old);
        Context.CustomerAllergies.AddRange(allergenIds.Distinct().Select(x=>new CustomerAllergy {
            Id=Guid.NewGuid(),CustomerId=customerId,AllergenId=x
        }));
        await SaveAsync();
    }
}

public sealed class ServiceCityRepository(HealthAppDbContext db) : EfRepository(db), IServiceCityRepository
{
    public async Task<IReadOnlyList<ServiceCity>> GetAllAsync() =>
        await Context.ServiceCities.AsNoTracking()
            .OrderBy(x => x.City)
            .ToListAsync();

    public async Task<IReadOnlyList<ServiceCity>> GetEnabledAsync() =>
        await Context.ServiceCities.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.City)
            .ToListAsync();

    public Task<ServiceCity?> GetByCityAsync(string city) =>
        Context.ServiceCities.FirstOrDefaultAsync(x => x.City == city);

    public Task<ServiceCity?> GetByIdAsync(Guid id) =>
        Context.ServiceCities.FirstOrDefaultAsync(x => x.Id == id);

    public async Task AddAsync(ServiceCity city)
    {
        Context.ServiceCities.Add(city);
        await Context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceCity city)
    {
        Context.ServiceCities.Update(city);
        await Context.SaveChangesAsync();
    }
}

public sealed class CityAreaRepository(HealthAppDbContext db) : EfRepository(db), ICityAreaRepository
{
    public async Task<IReadOnlyList<CityArea>> GetActiveAsync(string? city = null)
    {
        var q = Context.CityAreas.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(x => x.City == city);
        return await q.OrderBy(x => x.City).ThenBy(x => x.Name).ToListAsync();
    }
    public Task<CityArea?> GetAsync(Guid id) => Context.CityAreas.FirstOrDefaultAsync(x => x.Id == id);
    public async Task AddAsync(CityArea area) {
        Context.CityAreas.Add(area);
        await SaveAsync();
    }
}

public sealed class OutletDeliveryAreaRepository(HealthAppDbContext db) : EfRepository(db), IOutletDeliveryAreaRepository
{
    public async Task<IReadOnlyList<OutletDeliveryArea>> GetByOutletAsync(Guid outletId) => await Context.OutletDeliveryAreas.AsNoTracking().Where(x => x.OutletId == outletId && x.IsActive).OrderBy(x => x.CityAreaId).ToListAsync();
    public async Task<IReadOnlyList<CityArea>> GetAreasForOutletAsync(Guid outletId) => await (from oa in Context.OutletDeliveryAreas.AsNoTracking() join a in Context.CityAreas.AsNoTracking() on oa.CityAreaId equals a.Id where oa.OutletId == outletId && oa.IsActive && a.IsActive orderby a.Name select a).ToListAsync();
    public async Task ReplaceAsync(Guid outletId, IEnumerable<OutletDeliveryArea> areas)
    {
        var existing = await Context.OutletDeliveryAreas.Where(x => x.OutletId == outletId).ToListAsync();
        Context.OutletDeliveryAreas.RemoveRange(existing);
        Context.OutletDeliveryAreas.AddRange(areas);
        await SaveAsync();
    }
}

public sealed class DeliveryPricingRepository(HealthAppDbContext db) : EfRepository(db), IDeliveryPricingRepository
{
    public async Task<IReadOnlyList<DeliveryPricingRule>> GetByOutletAsync(Guid outletId) => await Context.DeliveryPricingRules.AsNoTracking().Where(x => x.OutletId == outletId && x.IsActive).OrderBy(x => x.MaxDistanceKm).ToListAsync();
    public async Task AddAsync(DeliveryPricingRule rule) {
        Context.DeliveryPricingRules.Add(rule);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid id, Guid outletId) {
        var x = await Context.DeliveryPricingRules.FirstOrDefaultAsync(x => x.Id == id && x.OutletId == outletId);
        if (x is not null) {
            Context.DeliveryPricingRules.Remove(x);
            await SaveAsync();
        }
    }
}

public sealed class CustomerAddressRepository(HealthAppDbContext db) : EfRepository(db), ICustomerAddressRepository
{
    public async Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(Guid customerId) => await Context.CustomerAddresses.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Label).ToListAsync();
    public Task<CustomerAddress?> GetAsync(Guid customerId, Guid id) => Context.CustomerAddresses.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.Id == id);
    public async Task AddAsync(CustomerAddress address) {
        if (address.IsDefault) await ClearDefaults(address.CustomerId, null);
        Context.CustomerAddresses.Add(address);
        await SaveAsync();
    }
    public async Task UpdateAsync(CustomerAddress address) {
        if (address.IsDefault) await ClearDefaults(address.CustomerId, address.Id);
        Context.CustomerAddresses.Update(address);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid customerId, Guid id) {
        var x = await GetAsync(customerId,id);
        if (x is not null) {
            Context.CustomerAddresses.Remove(x);
            await SaveAsync();
        }
    }
    private async Task ClearDefaults(Guid customerId, Guid? except) {
        var rows = await Context.CustomerAddresses.Where(x => x.CustomerId == customerId && x.IsDefault && (!except.HasValue || x.Id != except.Value)).ToListAsync();
        foreach (var x in rows) x.IsDefault=false;
    }
}

public sealed class SubscriptionDiscountTierRepository(HealthAppDbContext db) : EfRepository(db), ISubscriptionDiscountTierRepository
{
    public async Task<IReadOnlyList<SubscriptionDiscountTier>> GetByOutletAsync(Guid outletId) => await Context.SubscriptionDiscountTiers.AsNoTracking().Where(x => x.OutletId == outletId && x.IsActive).OrderBy(x => x.MinMeals).ToListAsync();
    public async Task AddAsync(SubscriptionDiscountTier tier) {
        Context.SubscriptionDiscountTiers.Add(tier);
        await SaveAsync();
    }
    public async Task UpdateAsync(SubscriptionDiscountTier tier) {
        Context.SubscriptionDiscountTiers.Update(tier);
        await SaveAsync();
    }
    public async Task DeleteAsync(Guid outletId, Guid id) {
        var x = await Context.SubscriptionDiscountTiers.FirstOrDefaultAsync(x=>x.Id==id&&x.OutletId==outletId);
        if(x is not null) {
            Context.SubscriptionDiscountTiers.Remove(x);
            await SaveAsync();
        }
    }
}

public sealed class MealSelectionHistoryRepository(HealthAppDbContext db) : EfRepository(db), IMealSelectionHistoryRepository
{
    public async Task AddAsync(MealSelectionHistory history) {
        Context.MealSelectionHistories.Add(history);
        await SaveAsync();
    }
    public async Task<IReadOnlyList<MealSelectionHistory>> GetBySelectionAsync(Guid selectionId) => await Context.MealSelectionHistories.AsNoTracking().Where(x=>x.MealSelectionId==selectionId).OrderByDescending(x=>x.OccurredAtUtc).ToListAsync();
}

public sealed class PaymentSettlementReconciliationExceptionRepository(HealthAppDbContext db) : EfRepository(db), IPaymentSettlementReconciliationExceptionRepository
{
    public Task<PaymentSettlementReconciliationException?> GetOpenAsync(string provider, string providerPaymentId, string providerSettlementId, string exceptionType) =>
        Context.PaymentSettlementReconciliationExceptions.FirstOrDefaultAsync(x =>
            x.Provider == provider &&
            x.ProviderPaymentId == providerPaymentId &&
            x.ProviderSettlementId == providerSettlementId &&
            x.ExceptionType == exceptionType &&
            x.Status == "Open");

    public async Task AddAsync(PaymentSettlementReconciliationException exception)
    {
        Context.PaymentSettlementReconciliationExceptions.Add(exception);
        await SaveAsync();
    }

    public async Task UpdateAsync(PaymentSettlementReconciliationException exception)
    {
        Context.PaymentSettlementReconciliationExceptions.Update(exception);
        await SaveAsync();
    }

    private IQueryable<PaymentSettlementReconciliationException> OpenQuery(string? provider, Guid? outletId) =>
        Context.PaymentSettlementReconciliationExceptions.AsNoTracking()
            .Where(ex => ex.Status == "Open"
                && (string.IsNullOrWhiteSpace(provider) || ex.Provider == provider)
                && (!outletId.HasValue || Context.PaymentTransactions.AsNoTracking().Any(payment =>
                    payment.Provider == ex.Provider
                    && payment.ProviderPaymentId == ex.ProviderPaymentId
                    && payment.OutletId == outletId)));

    public Task<PaymentSettlementReconciliationException?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Context.PaymentSettlementReconciliationExceptions.FirstOrDefaultAsync(x => x.Id == id && x.Status == "Open", cancellationToken);

    public async Task<(IReadOnlyList<PaymentSettlementReconciliationException> Items, int TotalCount)> GetOpenPageAsync(
        string? provider, Guid? outletId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = OpenQuery(provider, outletId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((Math.Clamp(page, 1, 1_000_000) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100)).ToListAsync(cancellationToken);
        return (items, totalCount);
    }
}

public sealed class PaymentGatewaySettlementRepository(HealthAppDbContext db) : EfRepository(db), IPaymentGatewaySettlementRepository
{
    public Task<PaymentGatewaySettlement?> GetByPaymentTransactionAsync(Guid id) =>
        Context.PaymentGatewaySettlements.FirstOrDefaultAsync(x => x.PaymentTransactionId == id);

    public Task<PaymentGatewaySettlement?> GetByProviderPaymentIdAsync(string provider, string paymentId) =>
        Context.PaymentGatewaySettlements.FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderPaymentId == paymentId);

    public async Task AddAsync(PaymentGatewaySettlement settlement)
    {
        Context.PaymentGatewaySettlements.Add(settlement);
        await SaveAsync();
    }
    public Task UpdateAsync(PaymentGatewaySettlement settlement) { Context.PaymentGatewaySettlements.Update(settlement); return Task.CompletedTask; }

    private IQueryable<PaymentGatewaySettlement> UnreconciledQuery(Guid? outletId) =>
        Context.PaymentGatewaySettlements.AsNoTracking()
            .Where(x => x.Status != "Reconciled" && (!outletId.HasValue || x.OutletId == outletId));

    public async Task<(IReadOnlyList<PaymentGatewaySettlement> Items, int TotalCount)> GetUnreconciledPageAsync(
        Guid? outletId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = UnreconciledQuery(outletId);
        var totalCount = await query.CountAsync(cancellationToken);
        var safePage = Math.Clamp(page, 1, 1_000_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var items = await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync(cancellationToken);
        return (items, totalCount);
    }
}

public sealed class PaymentTransactionRepository(HealthAppDbContext db) : EfRepository(db), IPaymentTransactionRepository
{
    public Task<PaymentTransaction?> GetAsync(Guid id) =>
        Context.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == id);

    public Task<PaymentTransaction?> GetLatestBySubscriptionAsync(Guid subscriptionId) =>
        Context.PaymentTransactions.AsNoTracking()
            .Where(x => x.SubscriptionId == subscriptionId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string provider, string key) =>
        Context.PaymentTransactions.FirstOrDefaultAsync(x => x.Provider == provider && x.IdempotencyKey == key);

    public Task<PaymentTransaction?> GetByProviderOrderIdAsync(string providerOrderId) =>
        Context.PaymentTransactions.FirstOrDefaultAsync(x => x.ProviderOrderId == providerOrderId);

    public Task<PaymentTransaction?> GetByProviderPaymentIdAsync(string provider, string providerPaymentId) =>
        Context.PaymentTransactions.FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderPaymentId == providerPaymentId);

    public Task<PaymentTransaction?> GetByOnboardingApplicationIdAsync(Guid applicationId) =>
        Context.PaymentTransactions
            .Where(x => x.OutletOnboardingApplicationId == applicationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<PaymentTransaction>> GetRetryableAsync(DateTime utcNow, DateTime staleClaimBeforeUtc, int maxAttempts, int batchSize) =>
        await Context.PaymentTransactions
            .Where(x => x.Status == "Pending" &&
                        x.PaymentType == "CustomerSubscription" &&
                        x.AttemptCount < maxAttempts &&
                        ((x.ProcessingStatus == "ProviderOrderCreationFailed" &&
                          x.NextRetryAtUtc != null && x.NextRetryAtUtc <= utcNow) ||
                         (x.ProcessingStatus == "RetryingProviderOrder" &&
                          x.LastAttemptAtUtc != null && x.LastAttemptAtUtc <= staleClaimBeforeUtc)))
            .OrderBy(x => x.NextRetryAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync();

    public async Task<bool> TryClaimRetryAsync(Guid paymentId, DateTime utcNow, int maxAttempts)
    {
        var affected = await Context.PaymentTransactions
            .Where(x => x.Id == paymentId &&
                        x.Status == "Pending" &&
                        x.AttemptCount < maxAttempts &&
                        ((x.ProcessingStatus == "ProviderOrderCreationFailed" &&
                          x.NextRetryAtUtc != null && x.NextRetryAtUtc <= utcNow) ||
                         (x.ProcessingStatus == "RetryingProviderOrder" &&
                          x.LastAttemptAtUtc != null && x.LastAttemptAtUtc <= utcNow)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, "RetryingProviderOrder")
                .SetProperty(x => x.LastAttemptAtUtc, utcNow));

        return affected == 1;
    }

    // Atomic lease: only one request may verify/fulfil a provider order at a time.
    // A crashed worker becomes reclaimable after the lease expires.
    public async Task<bool> TryClaimPaymentProcessingAsync(Guid paymentId, DateTime utcNow, DateTime staleClaimBeforeUtc)
    {
        var affected = await Context.PaymentTransactions
            .Where(x => x.Id == paymentId &&
                        (x.ProcessingStatus != "WebhookProcessing" ||
                         x.LastAttemptAtUtc == null ||
                         x.LastAttemptAtUtc <= staleClaimBeforeUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, "WebhookProcessing")
                .SetProperty(x => x.LastAttemptAtUtc, utcNow));

        return affected == 1;
    }

    public async Task AddAsync(PaymentTransaction payment)
    {
        Context.PaymentTransactions.Add(payment);
        await SaveAsync();
    }

    public async Task UpdateAsync(PaymentTransaction payment)
    {
        Context.PaymentTransactions.Update(payment);
        await SaveAsync();
    }
}

public sealed class DiscountCodeRepository(HealthAppDbContext db) : EfRepository(db), IDiscountCodeRepository
{
    public Task<DiscountCode?> GetAsync(Guid? outletId, string code) => Context.DiscountCodes.FirstOrDefaultAsync(x => x.OutletId == outletId && x.Code == code.Trim().ToUpperInvariant() && x.IsActive);
    public async Task<IReadOnlyList<DiscountCode>> GetByOutletAsync(Guid outletId) => await Context.DiscountCodes.AsNoTracking().Where(x=>x.OutletId==outletId).OrderBy(x=>x.Code).ToListAsync();
    public async Task AddAsync(DiscountCode code) {
        Context.DiscountCodes.Add(code);
        await SaveAsync();
    }
    public async Task UpdateAsync(DiscountCode code) {
        Context.DiscountCodes.Update(code);
        await SaveAsync();
    }
}

public sealed class OrderFinancialRepository(HealthAppDbContext db) : EfRepository(db), IOrderFinancialRepository
{
    public async Task AddAsync(OrderFinancialBreakdown breakdown) {
        Context.OrderFinancialBreakdowns.Add(breakdown);
        await SaveAsync();
    }
    public Task<OrderFinancialBreakdown?> GetByOrderAsync(Guid orderId) => Context.OrderFinancialBreakdowns.FirstOrDefaultAsync(x=>x.OrderId==orderId);
}
