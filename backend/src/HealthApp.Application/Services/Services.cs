using HealthApp.Application.Abstractions;
using HealthApp.Application.Events;
using HealthApp.Application.Orchestration;
using HealthApp.Application.Strategies;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Domain.Events;
using HealthApp.Shared.DTOs;
namespace HealthApp.Application.Services;
public sealed class AuthService(IUserRepository users, ITokenService tokens, IPasswordService passwords) : IAuthService
{
    public async Task<AuthResponse?> LoginAsync(LoginRequest r)
    {
        var user = await users.FindByEmailAsync(r.Email);
        if (user is null || !user.IsActive || !passwords.Verify(r.Password, user.PasswordHash))
            return null;
        if (user.IsDemo && user.DemoExpiresAtUtc.HasValue && user.DemoExpiresAtUtc.Value <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Your 7-day demo has expired. Request a new demo account to continue exploring HealthApp.");
        return tokens.CreateToken(user);
    }
    public async Task<AuthResponse> RegisterAsync(RegisterRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 8) throw new ArgumentException("Password must be at least 8 characters.");
        if (await users.FindByEmailAsync(r.Email) is not null) throw new InvalidOperationException("Email is already registered.");
        var mobileDigits = new string((r.MobileNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (mobileDigits.StartsWith("91") && mobileDigits.Length == 12) mobileDigits = mobileDigits[2..];
        if (!System.Text.RegularExpressions.Regex.IsMatch(mobileDigits, "^[6-9]\\d{9}$"))
            throw new ArgumentException("Enter a valid 10-digit Indian mobile number.");
        var normalizedMobile = "+91" + mobileDigits;
        if (await users.FindByMobileAsync(normalizedMobile) is not null)
            throw new InvalidOperationException("Mobile number is already registered.");
        var role = Enum.TryParse<UserRole>(r.Role, true, out var parsed) ? parsed : UserRole.Customer;
        if (role is UserRole.SuperAdmin or UserRole.Driver) throw new UnauthorizedAccessException("This role cannot be self-registered.");
        if (role == UserRole.OutletAdmin)
            throw new UnauthorizedAccessException("Outlet administrators must complete outlet onboarding and verification before an account is activated.");
        Guid? outletId = null;
        var user = new User {
            Id = Guid.NewGuid(),
            Email = r.Email.Trim().ToLowerInvariant(),
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            MobileNumber = normalizedMobile,
            Role = role,
            OutletId = outletId,
            PasswordHash = passwords.Hash(r.Password)
        };
        await users.AddAsync(user);
        return tokens.CreateToken(user);
    }
}
public sealed class MarketplaceService(IOutletRepository outlets, IMealPlanRepository plans, IRecipeRepository recipes, IOutletMenuRepository menu, ISaaSPlanRepository saasPlans, IServiceCityRepository serviceCities) : IMarketplaceService
{
    public async Task<IReadOnlyList<SaaSPlanDto>> GetSaaSPlansAsync() => (await saasPlans.GetActiveAsync()).Select(Map).ToList();
    public async Task<AvailabilityResponse> GetAvailabilityAsync(double latitude, double longitude, string? city = null)
    {
        if (double.IsNaN(latitude) || double.IsInfinity(latitude) || latitude is < -90 or > 90 ||
            double.IsNaN(longitude) || double.IsInfinity(longitude) || longitude is < -180 or > 180)
            throw new ArgumentException("Map coordinates are invalid.");

        if (!string.IsNullOrWhiteSpace(city))
        {
            var serviceCity = await serviceCities.GetByCityAsync(city.Trim());
            if (serviceCity is null || !serviceCity.IsEnabled)
                return new(false, $"{city.Trim()} is not currently supported by HealthApp.", []);
        }

        var result = (await outlets.GetAllAsync())
            .Where(x => x.Status == OutletStatus.Live)
            .Where(x => string.IsNullOrWhiteSpace(city) || x.City.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(x => (outlet: x, distance: Distance(latitude, longitude, x.Latitude, x.Longitude)))
            .Where(x => x.distance <= x.outlet.ServiceRadiusKm)
            .Select(x => ToDto(x.outlet, x.distance))
            .OrderBy(x => x.DistanceKm)
            .ToList();

        return new(
            result.Count > 0,
            result.Count > 0
                ? $"{result.Count} outlet(s) can deliver to your location."
                : "No active outlet currently serves your location.",
            result);
    }
    public async Task<IReadOnlyList<CityDto>> GetCitiesAsync()
    {
        return (await serviceCities.GetEnabledAsync())
            .Select(x => new CityDto(x.City, x.State, 0))
            .OrderBy(x => x.City)
            .ToList();
    }
    public async Task<IReadOnlyList<OutletDto>> GetAllOutletsAsync(string? city = null)
    {
        var rows = (await outlets.GetAllAsync()).Where(x => x.Status == OutletStatus.Live);
        if (!string.IsNullOrWhiteSpace(city))
            rows = rows.Where(x => x.City.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase));
        return rows.Select(x => ToDto(x, 0)).ToList();
    }
    public async Task<OutletDto?> GetOutletAsync(string slug) {
        var x = await outlets.GetBySlugAsync(slug);
        return x is null || x.Status != OutletStatus.Live ? null : ToDto(x, 0);
    }
    public async Task<IReadOnlyList<MealPlanDto>> GetPlansAsync(Guid outletId) {
        var outlet = await outlets.GetByIdAsync(outletId);
        return outlet is null || outlet.Status != OutletStatus.Live ? [] : (await plans.GetByOutletAsync(outletId)).Where(x => x.IsActive).Select(Map).ToList();
    }
    public async Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(Guid outletId, string? category) {
        var outlet = await outlets.GetByIdAsync(outletId);
        return outlet is null || outlet.Status != OutletStatus.Live ? [] : (await recipes.GetByOutletAndCategoryAsync(outletId, category)).Where(x => x.IsActive).Select(Map).ToList();
    }
    public async Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(Guid outletId) {
        var outlet = await outlets.GetByIdAsync(outletId);
        return outlet is null || outlet.Status != OutletStatus.Live ? [] : await MapMenu(outletId, await menu.GetByOutletAsync(outletId));
    }
    private async Task<IReadOnlyList<MenuItemDto>> MapMenu(Guid outletId, IReadOnlyList<OutletMenuItem> items)
    {
        var rs = (await recipes.GetByOutletAsync(outletId)).ToDictionary(x => x.Id);
        return items.Where(x => x.IsAvailable).Select(x => rs.TryGetValue(x.RecipeId, out var r) ? new MenuItemDto(x.Id, x.OutletId, x.RecipeId, r.Name, x.DayOfWeek, x.MealSlot.ToString(), (int)x.MealSlot, r.PricePerMeal, r.LargePricePerMeal, r.Calories, r.ProteinGrams, r.Category.ToString(), r.ImageUrl, x.IsAvailable, x.DisplayOrder, r.CarbsGrams, r.FatGrams, r.FiberGrams) : null).Where(x => x is not null).Cast<MenuItemDto>().ToList();
    }
    private static SaaSPlanDto Map(SaaSPlan x) => new(x.Id, x.Name, x.MonthlyFee, x.AnnualFee, x.IncludedActiveCustomers, x.AdditionalCustomerFee, x.CustomerTransactionFeePercent, x.Description, x.IsActive);
    private static MealPlanDto Map(MealPlan x) => new(x.Id, x.OutletId, x.Name, x.Frequency, x.MealsPerDay, x.MealsPerWeek, x.Price, x.Currency, x.Description, x.IsActive);
    private static RecipeDto Map(Recipe x) => new(x.Id,x.OutletId,x.Name,x.Calories,x.ProteinGrams,x.CarbsGrams,x.FatGrams,x.Category.ToString(),x.PricePerMeal,x.LargePricePerMeal,x.Description,x.ImageUrl,x.Tags,x.IsActive,
    x.RecipeIngredients.OrderBy(i=>i.Ingredient.Name).Select(i=>new RecipeIngredientDto(i.IngredientId,i.Ingredient.Name,i.Quantity,i.Unit,i.Ingredient.Allergens.Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name)).OrderBy(a=>a.Name).ToList())).ToList(),
    x.RecipeAllergens.Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name)).Concat(x.RecipeIngredients.SelectMany(i=>i.Ingredient.Allergens).Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name))).GroupBy(a=>a.Id).Select(g=>g.First()).OrderBy(a=>a.Name).ToList(), x.FiberGrams);
    private static OutletDto ToDto(Outlet x, double distance) => new(x.Id, x.Name, x.Slug, x.Subdomain, x.City, x.State, x.Pincode, x.Status.ToString(), x.BillingPlan.ToString(), x.LogoUrl, x.HeroImageUrl, (x.HealthHighlights??string.Empty).Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList(), x.PrimaryColor, x.Status == OutletStatus.Live, Math.Round(distance, 1), x.Rating, x.ReviewCount, x.About, x.Latitude, x.Longitude);
    private static double Distance(double lat1,double lon1,double lat2,double lon2) {
        const double R=6371d;
        var p1=lat1*Math.PI/180d;
        var p2=lat2*Math.PI/180d;
        var dp=(lat2-lat1)*Math.PI/180d;
        var dl=(lon2-lon1)*Math.PI/180d;
        var a=Math.Sin(dp/2)*Math.Sin(dp/2)+Math.Cos(p1)*Math.Cos(p2)*Math.Sin(dl/2)*Math.Sin(dl/2);
        return R*2*Math.Atan2(Math.Sqrt(a),Math.Sqrt(1-a));
    }
}
public sealed class CustomerService(
ICurrentUser current, IUserRepository users, ISubscriptionRepository subs, ISubscriptionMealSelectionRepository selections,
ICustomerCreditRepository credits, IOrderRepository orders, IOutletRepository outlets, IRecipeRepository recipes,
IOutletMenuRepository menu, IMealPlanRepository mealPlans, IOutletSubscriptionRepository outletSubscriptions,
IPlatformServiceFeeStrategy platformFee, ITaxStrategy taxStrategy, IPackageDiscountStrategy discountStrategy,
IDeliveryModeStrategyFactory deliveryModeFactory, IMealPriceStrategy mealPrice, ILateSkipFeePolicy lateSkipPolicy,
IPlatformTransactionRepository transactions, IDomainEventDispatcher events, IUnitOfWork unitOfWork,
ICustomerAddressRepository addresses, ISubscriptionDiscountTierRepository discountTiers, IMealSelectionHistoryRepository selectionHistory,
IDeliveryCalculator deliveryCalculator, IDiscountCodeRepository discountCodes, IOrderFinancialRepository orderFinancials, IDeliveryRepository deliveries, IAllergySafetyService allergySafety, IPaymentTransactionRepository payments) : ICustomerService
{
    public async Task<UserDto?> GetProfileAsync()
    {
        if (current.UserId is not Guid id) return null;
        var x = await users.FindByIdAsync(id);
        return x is null ? null : new(x.Id, x.Email, x.FirstName, x.LastName, x.Role.ToString(), x.OutletId, false, null, x.MobileNumber);
    }
    public async Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync()
    {
        if (current.UserId is not Guid id) return [];
        var result = new List<SubscriptionDto>();
        foreach (var x in await subs.GetByCustomerAsync(id)) result.Add(await ToDto(x));
        return result;
    }
    public async Task<CustomerDashboardDto?> GetDashboardAsync()
    {
        if (current.UserId is not Guid customerId) return null;

        var today = DateTime.UtcNow.Date;
        var mondayOffset = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var weekStart = today.AddDays(-mondayOffset);
        var weekEnd = weekStart.AddDays(7);

        var allSubscriptions = await subs.GetByCustomerAsync(customerId);
        var activeSubscriptions = allSubscriptions.Where(x => x.Status == SubscriptionStatus.Active).ToList();
        var todayDeliveries = new List<CustomerDashboardDeliveryDto>();
        var todayMeals = new List<CustomerDashboardMealDto>();
        var deliveryDates = new HashSet<DateTime>();
        var mealsThisWeek = 0;
        var proteinThisWeek = 0;
        var caloriesThisWeek = 0;
        var subscriptionSavings = 0m;

        foreach (var subscription in activeSubscriptions)
        {
            var selectionRows = await selections.GetBySubscriptionAndDateRangeAsync(subscription.Id, weekStart, weekEnd);
            var recipeLookup = (await recipes.GetByOutletAsync(subscription.OutletId))
                .Where(x => x.IsActive)
                .ToDictionary(x => x.Id);
            var deliveryRows = await deliveries.GetBySubscriptionAsync(subscription.Id);

            subscriptionSavings += subscription.SubscriptionDiscountAmount;
            foreach (var delivery in deliveryRows.Where(x => x.ScheduledDate.Date >= weekStart && x.ScheduledDate.Date < weekEnd && x.Status != DeliveryStatus.Skipped))
                deliveryDates.Add(delivery.ScheduledDate.Date);

            var weekRows = selectionRows.Where(x =>
                x.Status != MealSelectionStatus.Skipped &&
                x.Status != MealSelectionStatus.Cancelled &&
                x.Status != MealSelectionStatus.Expired);

            foreach (var selection in weekRows)
            {
                mealsThisWeek++;
                if (recipeLookup.TryGetValue(selection.RecipeId, out var recipe))
                {
                    proteinThisWeek += recipe.ProteinGrams;
                    caloriesThisWeek += recipe.Calories;
                }

                if (selection.MealDate.Date != today || !recipeLookup.TryGetValue(selection.RecipeId, out recipe))
                    continue;

                var deliverySlot = subscription.DeliveryMode == SubscriptionDeliveryMode.OneDeliveryPerDay
                    ? MealSlot.Afternoon
                    : selection.MealSlot;
                var delivery = deliveryRows.FirstOrDefault(x =>
                    x.ScheduledDate.Date == today &&
                    x.MealSlot == deliverySlot &&
                    x.Status != DeliveryStatus.Skipped);

                todayMeals.Add(new CustomerDashboardMealDto(
                    selection.Id,
                    subscription.Id,
                    selection.MealDate,
                    selection.MealSlot.ToString(),
                    recipe.Name,
                    recipe.Category.ToString(),
                    recipe.ImageUrl,
                    recipe.Calories,
                    recipe.ProteinGrams,
                    selection.Status.ToString(),
                    selection.MealPrice,
                    delivery?.Id));
            }

            foreach (var delivery in deliveryRows.Where(x => x.ScheduledDate.Date == today && x.Status != DeliveryStatus.Skipped))
            {
                var address = delivery.DeliveryAddressId is Guid addressId
                    ? await addresses.GetAsync(customerId, addressId)
                    : null;
                var mealCount = subscription.DeliveryMode == SubscriptionDeliveryMode.OneDeliveryPerDay
                    ? selectionRows.Count(x =>
                        x.MealDate.Date == today &&
                        x.Status != MealSelectionStatus.Skipped &&
                        x.Status != MealSelectionStatus.Cancelled &&
                        x.Status != MealSelectionStatus.Expired)
                    : 1;

                todayDeliveries.Add(new CustomerDashboardDeliveryDto(
                    delivery.Id,
                    subscription.Id,
                    delivery.ScheduledDate,
                    delivery.MealSlot.ToString(),
                    GetCustomerDeliveryWindow(delivery.MealSlot),
                    delivery.Status.ToString(),
                    delivery.Address,
                    address?.Latitude ?? 0,
                    address?.Longitude ?? 0,
                    Math.Max(1, mealCount)));
            }
        }

        var activeSubscriptionDtos = new List<CustomerDashboardSubscriptionDto>();
        foreach (var subscription in activeSubscriptions)
        {
            var payment = await payments.GetLatestBySubscriptionAsync(subscription.Id);
            activeSubscriptionDtos.Add(new CustomerDashboardSubscriptionDto(
                subscription.Id,
                subscription.PlanName,
                subscription.DeliveryMode.ToString(),
                subscription.MealsPerWeek,
                subscription.TotalCharged,
                subscription.NextDeliveryDate,
                subscription.Status.ToString(),
                payment?.Status ?? "Pending"));
        }

        return new CustomerDashboardDto(
            todayDeliveries
                .GroupBy(x => x.DeliveryId)
                .Select(x => x.First())
                .OrderBy(x => x.ScheduledDate)
                .ThenBy(x => x.MealSlot)
                .ToList(),
            todayMeals.OrderBy(x => x.MealDate).ThenBy(x => x.MealSlot).ToList(),
            activeSubscriptionDtos,
            new CustomerDashboardBenefitsDto(
                mealsThisWeek,
                proteinThisWeek,
                caloriesThisWeek,
                subscriptionSavings,
                deliveryDates.Count,
                activeSubscriptions.Count));
    }

    private static string GetCustomerDeliveryWindow(MealSlot slot)
        => slot switch
        {
            MealSlot.Morning => "07:00–09:00",
            MealSlot.Afternoon => "12:00–14:00",
            MealSlot.Evening => "17:00–19:00",
            MealSlot.Night => "20:00–22:00",
            _ => "Scheduled"
        };


    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync() => current.UserId is not Guid id ? [] : (await orders.GetByCustomerAsync(id)).Select(x => new OrderDto(x.Id,x.CustomerId,x.OutletId,x.Total,x.Status.ToString(),x.DeliveryDate,x.Address)).ToList();
    public async Task<SubscriptionQuoteDto?> QuoteAsync(SubscriptionQuoteRequest r)
    {
        if (current.UserId is not Guid customerId) return null;
        var deliveryMode = Parse<SubscriptionDeliveryMode>(r.DeliveryMode, "delivery mode");
        var duration = Parse<SubscriptionDuration>(r.Duration, "duration");
        var outlet = await outlets.GetByIdAsync(r.OutletId) ?? throw new KeyNotFoundException("Outlet not found.");
        if (outlet.Status != OutletStatus.Live) throw new InvalidOperationException("Outlet is not live yet.");
        var deliveryCity = ValidateDeliveryCity(r.DeliveryCity, outlet.City);
        var rs = (await recipes.GetByOutletAsync(outlet.Id)).Where(x => x.IsActive).ToDictionary(x => x.Id);
        var menuItems = await menu.GetByOutletAsync(outlet.Id);
        var meals = BuildSelections(r.Selections, outlet.Id, menuItems, rs);
        ValidateDeliveryMode(deliveryMode, meals);
        ValidateSelectionWindow(duration, meals);
        ValidateConfiguredDeliveryDays(outlet.DeliveryDays, meals);
        var selectedRecipes = meals.Select(x=>rs[x.RecipeId]).DistinctBy(x=>x.Id).ToList();
        var allergyWarnings = await allergySafety.GetWarningsAsync(customerId, selectedRecipes);
        await ValidateDeliveryAddressesAsync(customerId, deliveryCity, meals);
        var tiers = await discountTiers.GetByOutletAsync(outlet.Id);
        var packageDiscount = discountStrategy.Calculate(new(duration, meals), tiers);
        var gross = Math.Round(meals.Sum(x => x.MealPrice), 2);
        var codeAmount = await CalculateDiscountCodeAmountAsync(outlet.Id, gross, r.DiscountCode);
        var totalDiscount = Math.Min(gross, packageDiscount.Amount + codeAmount);
        var discountedMealAmount = Math.Round(gross - totalDiscount, 2);
        var delivery = await CalculateDeliveryAsync(outlet.Id, deliveryMode, meals, customerId);
        var taxes = taxStrategy.Calculate(discountedMealAmount, 0m, outlet.RestaurantGstRate, outlet.RestaurantGstMode);
        var net = taxes.RestaurantTaxableAmount;
        var service = platformFee.Calculate(discountedMealAmount);
        taxes = taxStrategy.Calculate(discountedMealAmount, service, outlet.RestaurantGstRate, outlet.RestaurantGstMode);
        var commissionRate = await GetOutletCommissionAsync(outlet.Id);
        var commission = Math.Round(net * commissionRate, 2);
        var quotes = new List<DeliveryQuoteDto>();
        foreach (var addressId in meals.Select(x => x.AddressId!.Value).Distinct()) quotes.Add(await deliveryCalculator.QuoteAsync(outlet.Id, customerId, addressId));
        var payable = net + taxes.RestaurantAmount + delivery + service + taxes.PlatformAmount;
        return new(gross, gross == 0 ? 0 : Math.Round(totalDiscount / gross * 100m, 4), totalDiscount, net, taxes.RestaurantAmount, delivery, service, taxes.PlatformAmount, payable, commissionRate, commission, service + commission, quotes, allergyWarnings, allergyWarnings.Count>0 && !allergyWarnings.All(x=>(r.ConfirmedAllergyRecipeIds??[]).Contains(x.RecipeId)), taxes.RestaurantTaxableAmount, taxes.RestaurantRate, taxes.RestaurantMode.ToString());
    }
    public async Task<SubscriptionDto?> SubscribeAsync(CreateSubscriptionRequest r)
    {
        if (current.UserId is not Guid customerId) return null;
        var deliveryMode = Parse<SubscriptionDeliveryMode>(r.DeliveryMode, "delivery mode");
        _ = deliveryModeFactory.Create(deliveryMode);
        var duration = Parse<SubscriptionDuration>(r.Duration, "package duration");
        if (r.Selections is null || r.Selections.Count == 0) throw new ArgumentException("Add at least one meal to your package.");
        var outlet = await outlets.GetByIdAsync(r.OutletId) ?? throw new KeyNotFoundException("Outlet not found or unavailable.");
        if (outlet.Status != OutletStatus.Live) throw new KeyNotFoundException("Outlet not found or unavailable.");
        var deliveryCity = ValidateDeliveryCity(r.DeliveryCity, outlet.City);
        var rs = (await recipes.GetByOutletAsync(outlet.Id)).Where(x => x.IsActive).ToDictionary(x => x.Id);
        var mealEntities = BuildSelections(r.Selections, outlet.Id, await menu.GetByOutletAsync(outlet.Id), rs);
        ValidateDeliveryMode(deliveryMode, mealEntities);
        ValidateSelectionWindow(duration, mealEntities);
        ValidateConfiguredDeliveryDays(outlet.DeliveryDays, mealEntities);
        var selectedRecipes = mealEntities.Select(x=>rs[x.RecipeId]).DistinctBy(x=>x.Id).ToList();
        await allergySafety.EnsureConfirmedAsync(customerId, selectedRecipes, r.ConfirmedAllergyRecipeIds);
        await ValidateDeliveryAddressesAsync(customerId, deliveryCity, mealEntities);
        var discount = discountStrategy.Calculate(new(duration, mealEntities), await discountTiers.GetByOutletAsync(outlet.Id));
        var gross = Math.Round(mealEntities.Sum(x => x.MealPrice), 2);
        var discountCodeResult = await CalculateDiscountCodeAsync(outlet.Id, gross, r.DiscountCode);
        var totalDiscount = Math.Min(gross, discount.Amount + discountCodeResult.Amount);
        var discountedMealAmount = Math.Round(gross - totalDiscount, 2);
        var delivery = await CalculateDeliveryAsync(outlet.Id, deliveryMode, mealEntities, customerId);
        var taxes = taxStrategy.Calculate(discountedMealAmount, 0m, outlet.RestaurantGstRate, outlet.RestaurantGstMode);
        var net = taxes.RestaurantTaxableAmount;
        var serviceFee = platformFee.Calculate(discountedMealAmount);
        taxes = taxStrategy.Calculate(discountedMealAmount, serviceFee, outlet.RestaurantGstRate, outlet.RestaurantGstMode);
        net = taxes.RestaurantTaxableAmount;
        var commissionRate = await GetOutletCommissionAsync(outlet.Id);
        var commission = Math.Round(net * commissionRate, 2);
        var start = mealEntities.Min(x => x.MealDate).Date;
        var end = duration switch {
            SubscriptionDuration.ThreeDays => start.AddDays(2),
            SubscriptionDuration.FiveDays => start.AddDays(4),
            SubscriptionDuration.OneWeek => start.AddDays(6),
            SubscriptionDuration.TwoWeeks => start.AddDays(13),
            SubscriptionDuration.OneMonth => start.AddDays(27),
            _ => start
        };
        var plan = (await mealPlans.GetByOutletAsync(outlet.Id)).FirstOrDefault(x => x.IsActive) ?? new MealPlan {
            Id=Guid.NewGuid(),
            OutletId=outlet.Id,
            Name="Custom Meal Package",
            Frequency=duration.ToString(),
            MealsPerDay=0,
            MealsPerWeek=mealEntities.Count,
            Price=0,
            Currency="INR",
            Description="Custom package"
        };
        if (plan.Price == 0 && plan.Name == "Custom Meal Package") await mealPlans.AddAsync(plan);
        var subscription = new Subscription
        {
            Id=Guid.NewGuid(),
            CustomerId=customerId,
            OutletId=outlet.Id,
            DeliveryCity=deliveryCity,
            MealPlanId=plan.Id,
            PlanName=$"{duration} Custom Meal Package",
            DeliveryMode=deliveryMode,
            Duration=duration,
            StartDate=start,
            EndDate=end,
            GrossMealAmount=gross,
            SubscriptionDiscountPercent=gross==0?0:Math.Round(totalDiscount/gross*100m,4),
            SubscriptionDiscountAmount=totalDiscount,
            NetMealAmount=net,
            PlatformServiceFee=serviceFee,
            PlatformServiceGst=taxes.PlatformAmount,
            PlatformServiceFeePercent=platformFee.Percent,
            PlatformServiceGstRate=taxes.PlatformRate,
            RestaurantGstRate=taxes.RestaurantRate,
            RestaurantGstMode=taxes.RestaurantMode,
            RestaurantTaxableAmount=taxes.RestaurantTaxableAmount,
            RestaurantGstAmount=taxes.RestaurantAmount,
            LateSkipFee=0,
            Price=net,
            DeliveryFee=delivery,
            CustomerTransactionFeePercent=0,
            TransactionFee=0,
            TotalCharged=net+taxes.RestaurantAmount+delivery+serviceFee+taxes.PlatformAmount,
            OutletAmount=net+taxes.RestaurantAmount-commission,
            OutletCommissionPercent=commissionRate,
            OutletCommissionAmount=commission,
            DiscountCode=discountCodeResult.AppliedCode,
            DiscountCodeAmount=discountCodeResult.Amount,
            TotalMealCount=mealEntities.Count,
            Frequency=string.IsNullOrWhiteSpace(r.Frequency)?"Weekly":r.Frequency.Trim(),
            MealsPerDay=0,
            MealsPerWeek=mealEntities.Count,
            Status=SubscriptionStatus.Active,
            NextDeliveryDate=start
        };
        foreach (var x in mealEntities) x.SubscriptionId = subscription.Id;
        await unitOfWork.ExecuteAsync(async () =>
        {
            await subs.AddAsync(subscription);
            await selections.AddRangeAsync(mealEntities);
            var order = new Order {
                Id=Guid.NewGuid(),CustomerId=customerId,OutletId=outlet.Id,SubscriptionId=subscription.Id,Total=subscription.TotalCharged,Status=OrderStatus.Pending,DeliveryDate=start,Address="Multiple scheduled delivery addresses"
            };
            await orders.AddAsync(order);
            await orderFinancials.AddAsync(new OrderFinancialBreakdown {
                Id=Guid.NewGuid(),OrderId=order.Id,GrossMealAmount=gross,DiscountAmount=totalDiscount,NetMealAmount=net,DeliveryAmount=delivery,PlatformServiceFee=serviceFee,PlatformServiceGst=taxes.PlatformAmount,RestaurantGstRate=taxes.RestaurantRate,RestaurantGstMode=taxes.RestaurantMode,RestaurantTaxableAmount=taxes.RestaurantTaxableAmount,RestaurantGstAmount=taxes.RestaurantAmount,LateSkipFee=0,CustomerPayable=subscription.TotalCharged,OutletCommission=commission,OutletCommissionGst=0,OutletSettlementAmount=subscription.OutletAmount,HealthAppRevenue=serviceFee+commission
            });
            var customer = await users.FindByIdAsync(customerId);
            var groups = deliveryMode == SubscriptionDeliveryMode.OneDeliveryPerDay ? mealEntities.GroupBy(x=>x.MealDate.Date).Select(g=>(IEnumerable<SubscriptionMealSelection>)g) : mealEntities.Select(x=>(IEnumerable<SubscriptionMealSelection>)new[] {
                x
            });
            foreach (var group in groups)
            {
                var first = group.First();  var address = await addresses.GetAsync(customerId, first.AddressId!.Value) ?? throw new InvalidOperationException("Delivery address could not be resolved.");
                await deliveries.AddAsync(new Delivery {
                    Id=Guid.NewGuid(),OrderId=order.Id,SubscriptionId=subscription.Id,OutletId=outlet.Id,CustomerId=customerId,DeliveryAddressId=address.Id,ScheduledDate=first.MealDate,MealSlot=deliveryMode==SubscriptionDeliveryMode.OneDeliveryPerDay?MealSlot.Afternoon:first.MealSlot,CustomerName=customer is null?"":$"{customer.FirstName} {customer.LastName}".Trim(),Address=$"{address.AddressLine1}, {address.AddressLine2}, {address.ContactPhone}".Trim(' ',','),DeliveryFee=first.DeliveryFee,Status=DeliveryStatus.Scheduled
                });
            }
            await transactions.AddAsync(new PlatformTransaction {
                Id=Guid.NewGuid(),CustomerId=customerId,OutletId=outlet.Id,SubscriptionId=subscription.Id,Type="CustomerSubscription",GrossAmount=subscription.TotalCharged,PlatformFee=serviceFee,OutletAmount=subscription.OutletAmount,FeePercent=platformFee.Percent,Currency="INR",Status="Pending"
            });
            await transactions.AddAsync(new PlatformTransaction {
                Id=Guid.NewGuid(),CustomerId=customerId,OutletId=outlet.Id,SubscriptionId=subscription.Id,Type="OutletCommission",GrossAmount=commission,PlatformFee=commission,OutletAmount=0,FeePercent=commissionRate*100m,Currency="INR",Status="Pending"
            });
            if (!string.IsNullOrWhiteSpace(discountCodeResult.AppliedCode)) {
                var dc=await discountCodes.GetAsync(outlet.Id,discountCodeResult.AppliedCode);  if (dc is not null) {
                    dc.RedemptionCount++; await discountCodes.UpdateAsync(dc);
                }
            }
            await events.PublishAsync(new SubscriptionCreatedEvent(subscription.Id,customerId,outlet.Id));
        });
        return await ToDto(subscription);
    }
    public async Task<IReadOnlyList<RecipeDto>> GetSubscriptionRecipesAsync(Guid subscriptionId,string? category) {
        var s=await GetOwnedSubscription(subscriptionId);
        return(await recipes.GetByOutletAndCategoryAsync(s.OutletId,category)).Where(x=>x.IsActive).Select(MapRecipe).ToList();
    }
    public async Task<IReadOnlyList<MenuItemDto>> GetSubscriptionMenuAsync(Guid subscriptionId)=>await MapMenu((await GetOwnedSubscription(subscriptionId)).OutletId);
    public async Task<IReadOnlyList<MealSelectionDto>> GetMealSelectionsAsync(Guid subscriptionId,DateTime? weekStart) {
        var s=await GetOwnedSubscription(subscriptionId);
        var from=(weekStart??s.StartDate).Date;
        return await MapSelections(await selections.GetBySubscriptionAndDateRangeAsync(s.Id,from,from.AddDays(7)));
    }
    public async Task<IReadOnlyList<MealSelectionDto>> SaveMealSelectionsAsync(Guid subscriptionId,SaveMealSelectionsRequest r)
    {
        var s=await GetOwnedSubscription(subscriptionId);
        if(s.Status!=SubscriptionStatus.Active)throw new InvalidOperationException("Only active subscriptions can be changed.");
        if(r.Selections.Count==0)throw new ArgumentException("At least one meal selection is required.");
        var recipeLookup=(await recipes.GetByOutletAsync(s.OutletId)).Where(x=>x.IsActive).ToDictionary(x=>x.Id);
        var newRows=BuildSelections(r.Selections,s.OutletId,await menu.GetByOutletAsync(s.OutletId),recipeLookup,s.Id);
        ValidateDeliveryMode(s.DeliveryMode,newRows);
        var selectedRecipes=newRows.Select(x=>recipeLookup[x.RecipeId]).DistinctBy(x=>x.Id).ToList();
        await allergySafety.EnsureConfirmedAsync(s.CustomerId,selectedRecipes,r.ConfirmedAllergyRecipeIds);
        foreach(var x in newRows) {
            if(!x.AddressId.HasValue)throw new ArgumentException("Every meal requires an address.");
            var q=await deliveryCalculator.QuoteAsync(s.OutletId,s.CustomerId,x.AddressId.Value);
            x.DeliveryFee=q.DeliveryFee;
        }
        var from=newRows.Min(x=>x.MealDate).Date;
        var to=newRows.Max(x=>x.MealDate).Date.AddDays(1);
        await unitOfWork.ExecuteAsync(async()=> {
            await selections.DeleteBySubscriptionAndDateRangeAsync(s.Id,from,to); await selections.AddRangeAsync(newRows);
        });
        return await MapSelections(await selections.GetBySubscriptionAndDateRangeAsync(s.Id,from,to));
    }
    public async Task<MealSelectionDto?> SkipMealAsync(Guid subscriptionId,Guid selectionId,SkipMealRequest r)
    {
        var s=await GetOwnedSubscription(subscriptionId);
        var outlet=await outlets.GetByIdAsync(s.OutletId)??throw new KeyNotFoundException("Outlet not found.");
        if(!outlet.AllowMealSkipping)throw new InvalidOperationException("This outlet does not allow meal skipping.");
        var item=await selections.GetAsync(selectionId)??throw new KeyNotFoundException("Meal selection not found.");
        if(item.SubscriptionId!=s.Id)throw new UnauthorizedAccessException("Meal selection does not belong to this subscription.");
        if(item.Status==MealSelectionStatus.Delivered)throw new InvalidOperationException("Delivered meals cannot be skipped.");
        if(item.Status!=MealSelectionStatus.Scheduled)throw new InvalidOperationException("This meal is no longer available to skip.");
        var now=DateTime.UtcNow;
        var late=lateSkipPolicy.IsLate(now,item.MealDate);
        var fee=lateSkipPolicy.GetFee(now,item.MealDate);
        item.Status=MealSelectionStatus.Unused;
        item.SkippedAtUtc=now;
        item.LateSkipFee=fee;
        await unitOfWork.ExecuteAsync(async()=> {
            await selections.UpdateAsync(item); var remaining=await selections.GetBySubscriptionAndDateRangeAsync(s.Id,item.MealDate.Date,item.MealDate.Date.AddDays(1)); if(!remaining.Any(x=>x.Status==MealSelectionStatus.Scheduled)) {
                foreach(var d in (await deliveries.GetBySubscriptionAsync(s.Id)).Where(x=>x.ScheduledDate.Date==item.MealDate.Date&&x.Status==DeliveryStatus.Scheduled)) {
                    d.Status=DeliveryStatus.Skipped; await deliveries.UpdateAsync(d);
                }
            }
            await selectionHistory.AddAsync(new MealSelectionHistory {
                Id=Guid.NewGuid(),MealSelectionId=item.Id,SubscriptionId=s.Id,Action="Skipped",OccurredAtUtc=now,FromMealDate=item.MealDate,Reason=r.Reason,Amount=fee
            }); if(late)await events.PublishAsync(new MealSkippedEvent(s.Id,item.Id,s.CustomerId,s.OutletId,item.MealPrice,item.DeliveryFee,true,fee,r.Reason));
        });
        return (await MapSelections(new[] {
            item
        })).FirstOrDefault();
    }
    public async Task<IReadOnlyList<MealSelectionDto>> SkipDayAsync(Guid subscriptionId,DateTime date,SkipDayRequest r)
    {
        var s=await GetOwnedSubscription(subscriptionId);
        var items=(await selections.GetBySubscriptionAndDateRangeAsync(s.Id,date.Date,date.Date.AddDays(1))).Where(x=>x.Status==MealSelectionStatus.Scheduled).ToList();
        if(items.Count==0)throw new InvalidOperationException("There are no scheduled meals to skip for this day.");
        var now=DateTime.UtcNow;
        await unitOfWork.ExecuteAsync(async()=> {
            foreach(var item in items) {
                var fee=lateSkipPolicy.GetFee(now,item.MealDate); item.Status=MealSelectionStatus.Unused; item.SkippedAtUtc=now; item.LateSkipFee=fee; await selections.UpdateAsync(item); await selectionHistory.AddAsync(new MealSelectionHistory {
                    Id=Guid.NewGuid(),MealSelectionId=item.Id,SubscriptionId=s.Id,Action="Skipped",OccurredAtUtc=now,FromMealDate=item.MealDate,Reason=r.Reason,Amount=fee
                }); if(fee>0)await events.PublishAsync(new MealSkippedEvent(s.Id,item.Id,s.CustomerId,s.OutletId,item.MealPrice,item.DeliveryFee,true,fee,r.Reason));
            }
            foreach(var d in (await deliveries.GetBySubscriptionAsync(s.Id)).Where(x=>x.ScheduledDate.Date==date.Date&&x.Status==DeliveryStatus.Scheduled)) {
                d.Status=DeliveryStatus.Skipped; await deliveries.UpdateAsync(d);
            }
        });
        return await MapSelections(items);
    }
    public async Task<MealSelectionDto?> RescheduleMealAsync(Guid subscriptionId,Guid selectionId,RescheduleMealRequest r)
    {
        var s=await GetOwnedSubscription(subscriptionId);
        var item=await selections.GetAsync(selectionId)??throw new KeyNotFoundException("Meal selection not found.");
        if(item.SubscriptionId!=s.Id)throw new UnauthorizedAccessException("Meal selection does not belong to this subscription.");
        if(item.Status!=MealSelectionStatus.Unused)throw new InvalidOperationException("Only unused meals can be rescheduled.");
        var newDate=r.NewMealDate.Date;
        if(newDate<DateTime.UtcNow.Date||newDate>s.EndDate.Date.AddDays(7))throw new ArgumentException("Reschedule date must be within the subscription and seven-day grace period.");
        if(!Enum.IsDefined(typeof(MealSlot),r.NewMealSlot))throw new ArgumentException("Invalid meal slot.");
        var addressId=r.AddressId??item.AddressId;
        if(!addressId.HasValue)throw new ArgumentException("A delivery address is required.");
        var outletMenu=await menu.GetByOutletAsync(s.OutletId);
        if(!outletMenu.Any(x=>x.DayOfWeek==newDate.DayOfWeek&&x.MealSlot==(MealSlot)r.NewMealSlot&&x.RecipeId==item.RecipeId&&x.IsAvailable))throw new ArgumentException("The selected meal is not available for the new date and slot.");
        var q=await deliveryCalculator.QuoteAsync(s.OutletId,s.CustomerId,addressId.Value);
        var oldDate=item.MealDate;
        item.Status=MealSelectionStatus.Rescheduled;
        item.RescheduledAtUtc=DateTime.UtcNow;
        var replacement=new SubscriptionMealSelection {
            Id=Guid.NewGuid(),
            SubscriptionId=s.Id,
            MealDate=newDate,
            MealSlot=(MealSlot)r.NewMealSlot,
            RecipeId=item.RecipeId,
            AddressId=addressId,
            PortionSize=item.PortionSize,
            Status=MealSelectionStatus.Scheduled,
            MealPrice=item.MealPrice,
            DeliveryFee=q.DeliveryFee,
            OriginalMealDate=oldDate,
            RescheduledFromSelectionId=item.Id
        };
        await unitOfWork.ExecuteAsync(async()=> {
            await selections.UpdateAsync(item); await selections.AddRangeAsync(new[] {
                replacement
            }); await selectionHistory.AddAsync(new MealSelectionHistory {
                Id=Guid.NewGuid(),MealSelectionId=item.Id,SubscriptionId=s.Id,Action="Rescheduled",OccurredAtUtc=DateTime.UtcNow,FromMealDate=oldDate,ToMealDate=newDate,Reason="Customer rescheduled unused meal",Amount=0
            }); var address=await addresses.GetAsync(s.CustomerId,addressId.Value); var customer=await users.FindByIdAsync(s.CustomerId); var existing=await deliveries.GetBySubscriptionAsync(s.Id); var reuse=s.DeliveryMode==SubscriptionDeliveryMode.OneDeliveryPerDay&&existing.Any(x=>x.ScheduledDate.Date==newDate&&x.Status==DeliveryStatus.Scheduled&&x.DeliveryAddressId==addressId.Value); if(!reuse&&address is not null)await deliveries.AddAsync(new Delivery {
                Id=Guid.NewGuid(),OrderId=existing.FirstOrDefault()?.OrderId??Guid.Empty,SubscriptionId=s.Id,OutletId=s.OutletId,CustomerId=s.CustomerId,DeliveryAddressId=address.Id,ScheduledDate=newDate,MealSlot=s.DeliveryMode==SubscriptionDeliveryMode.OneDeliveryPerDay?MealSlot.Afternoon:(MealSlot)r.NewMealSlot,CustomerName=customer is null?"":$"{customer.FirstName} {customer.LastName}".Trim(),Address=$"{address.AddressLine1}, {address.AddressLine2}, {address.ContactPhone}".Trim(' ',','),DeliveryFee=q.DeliveryFee,Status=DeliveryStatus.Scheduled
            }); await events.PublishAsync(new MealRescheduledEvent(s.Id,replacement.Id,oldDate,newDate,s.CustomerId,s.OutletId));
        });
        return (await MapSelections(new[] {
            replacement
        })).FirstOrDefault();
    }
    public async Task<CreditBalanceDto> GetCreditBalanceAsync()=>current.UserId is not Guid id?new(0):new(await credits.GetBalanceAsync(id));
    public async Task<IReadOnlyList<CreditTransactionDto>> GetCreditTransactionsAsync()=>current.UserId is not Guid id?[]:(await credits.GetTransactionsAsync(id)).Select(x=>new CreditTransactionDto(x.Id,x.Amount,x.Type.ToString(),x.Reason,x.CreatedAt)).ToList();
    private static T Parse<T>(string value,string label) where T:struct,
    Enum=>Enum.TryParse<T>(value,true,out var x)?x:throw new ArgumentException($"Invalid {label}.");
    private static string ValidateDeliveryCity(string? requestedCity,string outletCity) {
        var city=string.IsNullOrWhiteSpace(requestedCity)?outletCity:requestedCity.Trim();
        if(string.IsNullOrWhiteSpace(city))throw new ArgumentException("A delivery city is required.");
        if(!city.Equals(outletCity,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The selected delivery city '{city}' is not served by this outlet. Choose an outlet in {outletCity}.");
        return city;
    }
    private async Task ValidateDeliveryAddressesAsync(Guid customerId,string deliveryCity,IReadOnlyList<SubscriptionMealSelection> meals)
    {
        var addressIds=meals.Select(x=>x.AddressId).Distinct().ToList();
        if(addressIds.Any(x=>!x.HasValue))
            throw new ArgumentException("Every scheduled meal requires a delivery address.");
        foreach(var addressId in addressIds.Select(x=>x!.Value))
        {
            var address=await addresses.GetAsync(customerId,addressId)??throw new KeyNotFoundException("One or more delivery addresses were not found.");
            if(!address.City.Equals(deliveryCity,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"The package is for {deliveryCity}, but address {address.Label} is in {address.City}. Add or select an address in {deliveryCity}.");
        }
    }
    private async Task<Subscription> GetOwnedSubscription(Guid id) {
        var s=await subs.GetAsync(id)??throw new KeyNotFoundException("Subscription not found.");
        if(current.UserId is not Guid uid||s.CustomerId!=uid)throw new UnauthorizedAccessException("Subscription does not belong to the current customer.");
        return s;
    }
    private async Task<SubscriptionDto> ToDto(Subscription x)
    {
        var payment=await payments.GetLatestBySubscriptionAsync(x.Id);
        return new(x.Id,x.CustomerId,x.OutletId,x.MealPlanId,x.PlanName,x.DeliveryMode.ToString(),x.Price,x.DeliveryFee,x.CustomerTransactionFeePercent,x.TransactionFee,x.TotalCharged,x.OutletAmount,x.Frequency,x.MealsPerDay,x.MealsPerWeek,x.Status.ToString(),x.NextDeliveryDate,await credits.GetBalanceAsync(x.CustomerId),payment?.Status??"Pending",x.DeliveryCity,x.GrossMealAmount,x.SubscriptionDiscountAmount,x.RestaurantTaxableAmount,x.RestaurantGstAmount,x.RestaurantGstRate,x.RestaurantGstMode.ToString(),x.PlatformServiceFee,x.PlatformServiceGst,x.PackageStatus,x.IsOutletCreated,x.OutletDiscountType.ToString(),x.OutletDiscountValue,x.OutletDiscountReason);
    }
    private async Task<IReadOnlyList<MealSelectionDto>> MapSelections(IEnumerable<SubscriptionMealSelection> rows) {
        var result=new List<MealSelectionDto>();
        foreach(var x in rows) {
            var r=await recipes.GetAsync(x.RecipeId);
            result.Add(new(x.Id,x.SubscriptionId,x.MealDate,(int)x.MealSlot,x.RecipeId,r?.Name??"",r?.Category.ToString()??"",(int)x.PortionSize,x.Status.ToString(),x.MealPrice,x.DeliveryFee,x.LateSkipFee,x.SkippedAtUtc,x.RescheduledAtUtc));
        }
        return result;
    }
    private async Task<IReadOnlyList<MenuItemDto>> MapMenu(Guid outletId) {
        var rs=(await recipes.GetByOutletAsync(outletId)).ToDictionary(x=>x.Id);
        return(await menu.GetByOutletAsync(outletId)).Where(x=>x.IsAvailable).Select(x=>rs.TryGetValue(x.RecipeId,out var r)?new MenuItemDto(x.Id,x.OutletId,x.RecipeId,r.Name,x.DayOfWeek,x.MealSlot.ToString(),(int)x.MealSlot,r.PricePerMeal,r.LargePricePerMeal,r.Calories,r.ProteinGrams,r.Category.ToString(),r.ImageUrl,x.IsAvailable,x.DisplayOrder,r.CarbsGrams,r.FatGrams,r.FiberGrams):null).Where(x=>x is not null).Cast<MenuItemDto>().ToList();
    }
    private List<SubscriptionMealSelection> BuildSelections(IReadOnlyList<MealSelectionItem> items,Guid outletId,IReadOnlyList<OutletMenuItem> outletMenu,IReadOnlyDictionary<Guid,Recipe> rs,Guid? subscriptionId=null) {
        var result=new List<SubscriptionMealSelection>();
        foreach(var item in items) {
            if(!rs.TryGetValue(item.RecipeId,out var recipe))throw new ArgumentException("One or more selected meals are not available from this outlet.");
            if(!Enum.IsDefined(typeof(MealSlot),item.MealSlot))throw new ArgumentException("Invalid meal slot.");
            var slot=(MealSlot)item.MealSlot;
            if(!outletMenu.Any(x=>x.DayOfWeek==item.MealDate.DayOfWeek&&x.MealSlot==slot&&x.RecipeId==item.RecipeId&&x.IsAvailable))throw new ArgumentException($"{recipe.Name} is not available on {item.MealDate:dddd} at the selected slot.");
            var portion=Enum.IsDefined(typeof(MealPortionSize),item.PortionSize)?(MealPortionSize)item.PortionSize:MealPortionSize.Regular;
            result.Add(new SubscriptionMealSelection {
                Id=Guid.NewGuid(),SubscriptionId=subscriptionId??Guid.Empty,MealDate=item.MealDate.Date,MealSlot=slot,RecipeId=item.RecipeId,PortionSize=portion,Status=MealSelectionStatus.Scheduled,MealPrice=mealPrice.GetPrice(recipe,portion),AddressId=item.AddressId
            });
        }
        return result;
    }
    private static void ValidateSelectionWindow(SubscriptionDuration duration,IReadOnlyList<SubscriptionMealSelection> meals) {
        if(meals.Count==0)throw new ArgumentException("At least one meal is required.");
        var start=meals.Min(x=>x.MealDate).Date;
        var end=duration switch {
            SubscriptionDuration.ThreeDays=>start.AddDays(2),
            SubscriptionDuration.FiveDays=>start.AddDays(4),
            SubscriptionDuration.OneWeek=>start.AddDays(6),
            SubscriptionDuration.TwoWeeks=>start.AddDays(13),
            SubscriptionDuration.OneMonth=>start.AddDays(27),
            _=>start
        };
        if(meals.Any(x=>x.MealDate.Date<start||x.MealDate.Date>end))throw new ArgumentException("Selected meals are outside the package duration.");
        foreach(var g in meals.GroupBy(x=>(x.MealDate.Date-start).Days/7))if(g.Select(x=>x.MealDate.Date).Distinct().Count()>7)throw new ArgumentException("A package can contain at most seven active days in a week.");
    }
    private static void ValidateConfiguredDeliveryDays(string configuredDays, IReadOnlyList<SubscriptionMealSelection> meals)
    {
        var days = (configuredDays ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Enum.TryParse<DayOfWeek>(x, true, out var day) ? (DayOfWeek?)day : null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();

        if (days.Count == 0)
            throw new InvalidOperationException("This outlet has not configured its delivery days.");

        var invalid = meals.Select(x => x.MealDate.DayOfWeek).Distinct().Where(day => !days.Contains(day)).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException("One or more selected delivery dates are outside this outlet's configured delivery days.");
    }

    private static void ValidateDeliveryMode(SubscriptionDeliveryMode mode,IReadOnlyList<SubscriptionMealSelection> meals) {
        if(mode!=SubscriptionDeliveryMode.OneDeliveryPerDay)return;
        foreach(var g in meals.GroupBy(x=>x.MealDate.Date)) {
            if(g.Select(x=>x.AddressId).Distinct().Count()>1)throw new ArgumentException("One delivery per day requires the same address for all meals on that day.");
        }
    }
    private async Task<decimal> CalculateDeliveryAsync(Guid outletId,SubscriptionDeliveryMode mode,IReadOnlyList<SubscriptionMealSelection> meals,Guid customerId) {
        var total=0m;
        var groups=mode==SubscriptionDeliveryMode.OneDeliveryPerDay?meals.GroupBy(x=>x.MealDate.Date).Select(g=>(IEnumerable<SubscriptionMealSelection>)g):meals.Select(x=>(IEnumerable<SubscriptionMealSelection>)new[] {
            x
        });
        foreach(var g in groups) {
            var first=g.First();
            if(!first.AddressId.HasValue)throw new ArgumentException("Every scheduled meal requires an address.");
            var q=await deliveryCalculator.QuoteAsync(outletId,customerId,first.AddressId.Value);
            foreach(var x in g)x.DeliveryFee=q.DeliveryFee;
            total+=q.DeliveryFee;
        }
        return total;
    }
    private async Task<(decimal Amount,string? AppliedCode)> CalculateDiscountCodeAsync(Guid outletId,decimal gross,string? requestedCode) {
        if(string.IsNullOrWhiteSpace(requestedCode))return(0m,null);
        var code=await discountCodes.GetAsync(outletId,requestedCode.Trim());
        if(code is null)throw new ArgumentException("Discount code is invalid or inactive.");
        var now=DateTime.UtcNow;
        if(code.StartsAtUtc.HasValue&&now<code.StartsAtUtc.Value)throw new ArgumentException("Discount code is not active yet.");
        if(code.EndsAtUtc.HasValue&&now>code.EndsAtUtc.Value)throw new ArgumentException("Discount code has expired.");
        if(code.MaxRedemptions.HasValue&&code.RedemptionCount>=code.MaxRedemptions.Value)throw new ArgumentException("Discount code redemption limit has been reached.");
        var amount=Math.Round(gross*code.Percent/100m,2);
        if(code.MaxAmount.HasValue)amount=Math.Min(amount,code.MaxAmount.Value);
        return(amount,code.Code);
    }
    private async Task<decimal> CalculateDiscountCodeAmountAsync(Guid outletId,decimal gross,string? requestedCode)=>(await CalculateDiscountCodeAsync(outletId,gross,requestedCode)).Amount;
    private async Task<decimal> GetOutletCommissionAsync(Guid outletId) {
        var sub=await outletSubscriptions.GetByOutletAsync(outletId);
        return(sub?.TransactionFeePercent??0m)/100m;
    }
    private static RecipeDto MapRecipe(Recipe x)=>new(x.Id,x.OutletId,x.Name,x.Calories,x.ProteinGrams,x.CarbsGrams,x.FatGrams,x.Category.ToString(),x.PricePerMeal,x.LargePricePerMeal,x.Description,x.ImageUrl,x.Tags,x.IsActive,
    x.RecipeIngredients.OrderBy(i=>i.Ingredient.Name).Select(i=>new RecipeIngredientDto(i.IngredientId, i.Ingredient.Name, i.Quantity, i.Unit, i.Ingredient.Allergens.Select(a => new AllergenDto(a.AllergenId, a.Allergen.Name)).OrderBy(a => a.Name).ToList())).ToList(),
    x.RecipeAllergens.Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name)).Concat(x.RecipeIngredients.SelectMany(i=>i.Ingredient.Allergens).Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name))).GroupBy(a=>a.Id).Select(g=>g.First()).OrderBy(a=>a.Name).ToList());
}
public sealed class OutletService(ICurrentUser current,IOutletRepository outlets,IOutletSubscriptionRepository outletSubs,ISaaSPlanRepository saasPlans,IMealPlanRepository plans,IRecipeRepository recipes,IOutletMenuRepository menu,IUserRepository users,ISubscriptionRepository subscriptions,IOrderRepository orders,IDeliveryRepository deliveries,IIngredientRepository ingredients,IAllergenRepository allergens,ISubscriptionMealSelectionRepository selections,ICustomerAddressRepository addresses,ICityAreaRepository areas,IDeliveryLabelService deliveryLabels) : IOutletService
{
    public async Task<OutletTaxSettingsDto?> GetTaxSettingsAsync()
    {
        if (current.OutletId is not Guid id) return null;
        var outlet = await outlets.GetByIdAsync(id);
        return outlet is null ? null : new(outlet.RestaurantGstRate, outlet.RestaurantGstMode.ToString());
    }

    public async Task<OutletTaxSettingsDto?> UpdateTaxSettingsAsync(UpdateOutletTaxSettingsRequest request)
    {
        if (current.OutletId is not Guid id) return null;
        if (request.RestaurantGstRate < 0m || request.RestaurantGstRate > 100m)
            throw new ArgumentException("Restaurant GST rate must be between 0% and 100%.");
        if (!Enum.TryParse<GstMode>(request.RestaurantGstMode, true, out var mode))
            throw new ArgumentException("GST mode must be Inclusive or Exclusive.");

        var outlet = await outlets.GetByIdAsync(id) ?? throw new KeyNotFoundException("Outlet not found.");
        outlet.RestaurantGstRate = Math.Round(request.RestaurantGstRate, 4);
        outlet.RestaurantGstMode = mode;
        await outlets.UpdateAsync(outlet);
        return new(outlet.RestaurantGstRate, outlet.RestaurantGstMode.ToString());
    }

    public async Task<OutletDashboardDto> GetDashboardAsync()
    {
        if (current.OutletId is not Guid id)
            throw new UnauthorizedAccessException("The current user is not associated with an outlet.");

        var outlet = await GetCurrentAsync() ?? throw new KeyNotFoundException("Outlet not found.");
        var subscriptionsList = await subscriptions.GetByOutletAsync(id);
        var plansList = await plans.GetByOutletAsync(id);
        var recipesList = await recipes.GetByOutletAsync(id);
        var customersList = await users.GetAllAsync();
        var ordersList = await orders.GetByOutletAsync(id);
        var deliveriesList = await deliveries.GetByOutletAsync(id);

        var windowStart = DateTime.UtcNow.Date.AddDays(-6);
        var today = DateTime.UtcNow.Date;

        var customerIds = subscriptionsList
            .Where(x => x.Status == SubscriptionStatus.Active)
            .Select(x => x.CustomerId)
            .ToHashSet();

        var newSubscriptions = subscriptionsList
            .Where(x => x.StartDate.Date >= windowStart)
            .ToList();

        var newCustomers = subscriptionsList
            .GroupBy(x => x.CustomerId)
            .Count(g => g.Min(x => x.StartDate).Date >= windowStart);

        var recentSubscriptions = newSubscriptions
            .OrderByDescending(x => x.StartDate)
            .Take(8)
            .Select(x => new OutletDashboardSubscriptionDto(
                x.Id,
                x.CustomerId,
                customersList.FirstOrDefault(c => c.Id == x.CustomerId) is { } customer
                    ? $"{customer.FirstName} {customer.LastName}".Trim()
                    : "Customer",
                x.PlanName,
                x.StartDate,
                x.EndDate,
                x.MealsPerWeek,
                x.Status.ToString()))
            .ToList();

        var todayDeliveries = deliveriesList
            .Where(x => x.ScheduledDate.Date == today && x.Status != DeliveryStatus.Skipped)
            .ToList();

        var todayDeliveryPoints = todayDeliveries
            .Where(x => x.DeliveryAddressId.HasValue)
            .Select(x => x.DeliveryAddressId!.Value)
            .Distinct()
            .Count();

        var pendingStatuses = new[]
        {
            DeliveryStatus.Scheduled,
            DeliveryStatus.Preparing,
            DeliveryStatus.OutForDelivery
        };

        var todayPending = todayDeliveries.Count(x => pendingStatuses.Contains(x.Status));

        var todaySlots = Enum.GetValues<MealSlot>()
            .Select(slot =>
            {
                var rows = todayDeliveries.Where(x => x.MealSlot == slot).ToList();
                var window = slot switch
                {
                    MealSlot.Morning => "07:00–09:00",
                    MealSlot.Afternoon => "12:00–14:00",
                    MealSlot.Evening => "17:00–19:00",
                    MealSlot.Night => "20:00–22:00",
                    _ => "Scheduled"
                };

                return new OutletDashboardDeliverySlotDto(
                    slot.ToString(),
                    window,
                    rows.Count,
                    rows.Count(x => pendingStatuses.Contains(x.Status)),
                    rows.Count(x => x.Status == DeliveryStatus.Delivered));
            })
            .ToList();

        var sales7d = ordersList
            .Where(x => x.DeliveryDate.Date >= windowStart && x.Status != OrderStatus.Cancelled)
            .Sum(x => x.Total);

        var todayLabels = await deliveryLabels.GetLabelsAsync(today);

        return new OutletDashboardDto(
            outlet,
            plansList.Count(x => x.IsActive),
            recipesList.Count(x => x.IsActive),
            customerIds.Count,
            subscriptionsList.Count(x => x.Status == SubscriptionStatus.Active),
            newCustomers,
            newSubscriptions.Count,
            ordersList.Count(x => x.DeliveryDate.Date >= windowStart && x.Status != OrderStatus.Cancelled),
            Math.Round(sales7d, 2),
            todayLabels.Count,
            todayDeliveries.Count,
            todayDeliveryPoints,
            todayPending,
            todaySlots,
            recentSubscriptions);
    }

    public async Task<OutletSubscriptionDetailDto?> GetSubscriptionDetailAsync(Guid subscriptionId)
    {
        if(current.OutletId is not Guid outletId)
            return null;

        var subscription=await subscriptions.GetAsync(subscriptionId);
        if(subscription is null||subscription.OutletId!=outletId)
            return null;

        var customer=await users.FindByIdAsync(subscription.CustomerId);
        var rows=await selections.GetBySubscriptionAsync(subscriptionId);
        var meals=new List<OutletSubscriptionMealDto>();

        foreach(var row in rows)
        {
            var recipe=await recipes.GetAsync(row.RecipeId);
            var address=row.AddressId.HasValue
                ? await addresses.GetAsync(subscription.CustomerId,row.AddressId.Value)
                : null;
            var area=address?.CityAreaId is Guid areaId ? await areas.GetAsync(areaId) : null;
            var slot=GetMealSlotInfo(row.MealSlot);

            meals.Add(new OutletSubscriptionMealDto(
                row.Id,
                row.MealDate,
                (int)row.MealSlot,
                slot.Name,
                slot.Window,
                row.RecipeId,
                recipe?.Name??"Meal",
                recipe?.Category.ToString()??"",
                row.PortionSize.ToString(),
                row.MealPrice,
                row.DeliveryFee,
                address?.Label??"",
                address is null
                    ? ""
                    : $"{address.AddressLine1}, {address.AddressLine2}".Trim(' ',','),
                area?.Name??"",
                area?.Pincode??"",
                address?.ContactPhone??"",
                row.Status.ToString()));
        }

        return new OutletSubscriptionDetailDto(
            subscription.Id,
            subscription.CustomerId,
            customer is null ? "Customer" : $"{customer.FirstName} {customer.LastName}".Trim(),
            customer?.Email??"",
            subscription.PlanName,
            subscription.DeliveryMode.ToString(),
            subscription.Duration.ToString(),
            subscription.StartDate,
            subscription.EndDate,
            subscription.Frequency,
            subscription.MealsPerDay,
            subscription.MealsPerWeek,
            subscription.GrossMealAmount,
            subscription.SubscriptionDiscountAmount,
            subscription.RestaurantGstAmount,
            Math.Round(subscription.NetMealAmount+subscription.RestaurantGstAmount,2),
            subscription.DeliveryFee,
            subscription.Status.ToString(),
            meals,
            subscription.RestaurantTaxableAmount,
            subscription.RestaurantGstRate,
            subscription.RestaurantGstMode.ToString());
    }

    public async Task<OutletKitchenDayDto> GetKitchenDayAsync(DateTime date)
    {
        if(current.OutletId is not Guid id)
            throw new UnauthorizedAccessException("The current user is not associated with an outlet.");

        var outlet=await GetCurrentAsync()??throw new KeyNotFoundException("Outlet not found.");
        var labels=await deliveryLabels.GetLabelsAsync(date.Date);
        var production=labels
            .GroupBy(x=>new { x.MealName, x.Category, x.PortionSize })
            .Select(g=>new OutletKitchenMealCountDto(g.Key.MealName,g.Key.Category,g.Key.PortionSize,g.Count()))
            .OrderByDescending(x=>x.Quantity)
            .ThenBy(x=>x.MealName)
            .ToList();

        return new OutletKitchenDayDto(
            date.Date,
            outlet.Name,
            outlet.LogoUrl,
            labels.Count,
            labels.Select(x=>x.CustomerName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            labels.Select(x=>x.SubscriptionId).Distinct().Count(),
            production,
            labels);
    }

    private static (string Name,string Window) GetMealSlotInfo(MealSlot slot) =>
        slot switch
        {
            MealSlot.Morning => ("Morning","07:00 - 09:00"),
            MealSlot.Afternoon => ("Afternoon","12:00 - 14:00"),
            MealSlot.Evening => ("Evening","17:00 - 19:00"),
            MealSlot.Night => ("Night","20:00 - 22:00"),
            _ => (slot.ToString(),"")
        };

    public async Task<OutletDto?> GetCurrentAsync() {
        if(current.OutletId is not Guid id)return null;
        var x=await outlets.GetByIdAsync(id);
        return x is null?null:new(x.Id,x.Name,x.Slug,x.Subdomain,x.City,x.State,x.Pincode,x.Status.ToString(),x.BillingPlan.ToString(),x.LogoUrl,x.HeroImageUrl??string.Empty,(x.HealthHighlights??string.Empty).Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList(),x.PrimaryColor,x.Status==OutletStatus.Live,0);
    }
    public async Task<OutletBillingDto?> GetBillingAsync() {
        if(current.OutletId is not Guid id)return null;
        var os=await outletSubs.GetByOutletAsync(id);
        if(os is null)return null;
        var plan=await saasPlans.GetAsync(os.SaaSPlanId);
        if(plan is null)return null;
        var count=(await subscriptions.GetByOutletAsync(id)).Count;
        var extra=Math.Max(0,count-plan.IncludedActiveCustomers)*plan.AdditionalCustomerFee;
        return new(id,plan.Id,plan.Name,os.BillingCycle,os.SubscriptionFee,os.SetupFee,os.TransactionFeePercent,count,plan.IncludedActiveCustomers,plan.AdditionalCustomerFee,extra,os.RenewalDate,os.Status);
    }
    public async Task<IReadOnlyList<SaaSPlanDto>> GetSaaSPlansAsync()=>(await saasPlans.GetActiveAsync()).Select(x=>new SaaSPlanDto(x.Id,x.Name,x.MonthlyFee,x.AnnualFee,x.IncludedActiveCustomers,x.AdditionalCustomerFee,x.CustomerTransactionFeePercent,x.Description,x.IsActive)).ToList();
    public async Task<OutletBillingDto?> ChangeSubscriptionAsync(ChangeOutletSubscriptionRequest r) {
        if(current.OutletId is not Guid id)return null;
        var p=await saasPlans.GetAsync(r.SaaSPlanId)??throw new KeyNotFoundException("SaaS plan not found.");
        var existing=await outletSubs.GetByOutletAsync(id);
        var os=existing??new OutletSubscription {
            Id=Guid.NewGuid(),
            OutletId=id
        };
        os.SaaSPlanId=p.Id;
        os.BillingCycle=r.BillingCycle;
        os.SubscriptionFee=r.BillingCycle.Equals("Annual",StringComparison.OrdinalIgnoreCase)?p.AnnualFee:p.MonthlyFee;
        if(os.SetupFee<=0)os.SetupFee=5000m;
        os.TransactionFeePercent=p.CustomerTransactionFeePercent;
        os.StartDate=DateTime.UtcNow.Date;
        os.RenewalDate=os.StartDate.AddMonths(r.BillingCycle.Equals("Annual",StringComparison.OrdinalIgnoreCase)?12:1);
        os.Status="Active";
        if(existing is null)await outletSubs.AddAsync(os);
        else await outletSubs.UpdateAsync(os);
        return await GetBillingAsync();
    }
    public async Task<IReadOnlyList<MealPlanDto>> GetPlansAsync()=>current.OutletId is not Guid id?[]:(await plans.GetByOutletAsync(id)).Where(x=>x.IsActive).Select(x=>new MealPlanDto(x.Id,x.OutletId,x.Name,x.Frequency,x.MealsPerDay,x.MealsPerWeek,x.Price,x.Currency,x.Description,x.IsActive)).ToList();
    public async Task<MealPlanDto?> CreatePlanAsync(CreateMealPlanRequest r) {
        if(current.OutletId is not Guid id)return null;
        var x=new MealPlan {
            Id=Guid.NewGuid(),
            OutletId=id,
            Name=r.Name,
            Frequency=r.Frequency,
            MealsPerDay=r.MealsPerDay,
            MealsPerWeek=r.MealsPerDay*7,
            Price=r.Price,
            Description=r.Description
        };
        await plans.AddAsync(x);
        return new(x.Id,x.OutletId,x.Name,x.Frequency,x.MealsPerDay,x.MealsPerWeek,x.Price,x.Currency,x.Description,x.IsActive);
    }
    public async Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(string? category) => current.OutletId is not Guid id
        ? []
        : (await recipes.GetByOutletAndCategoryAsync(id, category)).Select(Map).ToList();
    public async Task<RecipeDto?> CreateRecipeAsync(CreateRecipeRequest r) {
        if(current.OutletId is not Guid id)return null;
        var cat=Enum.TryParse<RecipeCategory>(r.Category,true,out var c)?c:RecipeCategory.Veg;
        var ingredientIds=(r.Ingredients??[]).Select(x=>x.IngredientId).Distinct().ToList();
        var allergenIds=(r.AllergenIds??[]).Distinct().ToList();
        var validIngredients=await ingredients.GetByIdsAsync(ingredientIds);
        if(validIngredients.Count!=ingredientIds.Count)throw new ArgumentException("One or more ingredients are invalid.");
        var validAllergens=await allergens.GetByIdsAsync(allergenIds);
        if(validAllergens.Count!=allergenIds.Count)throw new ArgumentException("One or more allergens are invalid.");
        var x=new Recipe {
            Id=Guid.NewGuid(),
            OutletId=id,
            Name=r.Name.Trim(),
            Category=cat,
            Calories=r.Calories,
            ProteinGrams=r.ProteinGrams,
            CarbsGrams=r.CarbsGrams,
            FatGrams=r.FatGrams,
            FiberGrams=r.FiberGrams,
            PricePerMeal=r.PricePerMeal,
            LargePricePerMeal=r.LargePricePerMeal,
            Description=r.Description,
            ImageUrl=r.ImageUrl,
            Tags=r.Tags
        };
        foreach(var i in r.Ingredients??[])x.RecipeIngredients.Add(new RecipeIngredient {
            Id=Guid.NewGuid(),RecipeId=x.Id,IngredientId=i.IngredientId,Quantity=i.Quantity,Unit=string.IsNullOrWhiteSpace(i.Unit)?validIngredients.First(v=>v.Id==i.IngredientId).DefaultUnit:i.Unit.Trim()
        });
        foreach(var a in allergenIds)x.RecipeAllergens.Add(new RecipeAllergen {
            RecipeId=x.Id,AllergenId=a
        });
        await recipes.AddAsync(x);
        return (await recipes.GetAsync(x.Id)) is { } saved
            ? Map(saved)
            : Map(x);
    }
    public async Task<RecipeDto?> UpdateRecipeAsync(Guid id,UpdateRecipeRequest r) {
        if(current.OutletId is not Guid outletId)return null;
        var x=await recipes.GetAsync(id);
        if(x is null||x.OutletId!=outletId)return null;
        var ingredientIds=(r.Ingredients??[]).Select(v=>v.IngredientId).Distinct().ToList();
        var allergenIds=(r.AllergenIds??[]).Distinct().ToList();
        var validIngredients=await ingredients.GetByIdsAsync(ingredientIds);
        if(validIngredients.Count!=ingredientIds.Count)throw new ArgumentException("One or more ingredients are invalid.");
        var validAllergens=await allergens.GetByIdsAsync(allergenIds);
        if(validAllergens.Count!=allergenIds.Count)throw new ArgumentException("One or more allergens are invalid.");
        x.Name=r.Name.Trim();
        x.Category=Enum.TryParse<RecipeCategory>(r.Category,true,out var c)?c:x.Category;
        x.Calories=r.Calories;
        x.ProteinGrams=r.ProteinGrams;
        x.CarbsGrams=r.CarbsGrams;
        x.FatGrams=r.FatGrams;
        x.FiberGrams=r.FiberGrams;
        x.PricePerMeal=r.PricePerMeal;
        x.LargePricePerMeal=r.LargePricePerMeal;
        x.Description=r.Description;
        x.ImageUrl=r.ImageUrl;
        x.Tags=r.Tags;
        x.IsActive=r.IsActive;
        x.RecipeIngredients.Clear();
        x.RecipeAllergens.Clear();
        foreach(var i in r.Ingredients??[])x.RecipeIngredients.Add(new RecipeIngredient {
            Id=Guid.NewGuid(),RecipeId=x.Id,IngredientId=i.IngredientId,Quantity=i.Quantity,Unit=string.IsNullOrWhiteSpace(i.Unit)?validIngredients.First(v=>v.Id==i.IngredientId).DefaultUnit:i.Unit.Trim()
        });
        foreach(var a in allergenIds)x.RecipeAllergens.Add(new RecipeAllergen {
            RecipeId=x.Id,AllergenId=a
        });
        await recipes.UpdateAsync(x);
        return (await recipes.GetAsync(x.Id)) is { } saved
            ? Map(saved)
            : Map(x);
    }
    public async Task<bool> DeleteRecipeAsync(Guid id) {
        if(current.OutletId is not Guid outletId)return false;
        if(!(await recipes.GetByOutletAsync(outletId)).Any(x=>x.Id==id))return false;
        await recipes.DeleteAsync(id);
        return true;
    }
    public async Task<IReadOnlyList<MenuItemDto>> GetMenuAsync() {
        if(current.OutletId is not Guid id)return[];
        var rs=(await recipes.GetByOutletAsync(id)).ToDictionary(x=>x.Id);
        return(await menu.GetByOutletAsync(id)).Select(x=>rs.TryGetValue(x.RecipeId,out var r)?new MenuItemDto(x.Id,x.OutletId,x.RecipeId,r.Name,x.DayOfWeek,x.MealSlot.ToString(),(int)x.MealSlot,r.PricePerMeal,r.LargePricePerMeal,r.Calories,r.ProteinGrams,r.Category.ToString(),r.ImageUrl,x.IsAvailable,x.DisplayOrder):null).Where(x=>x is not null).Cast<MenuItemDto>().ToList();
    }
    public async Task<IReadOnlyList<MenuItemDto>> SaveMenuAsync(BulkMenuRequest r) {
        if(current.OutletId is not Guid id)return[];
        var valid=(await recipes.GetByOutletAsync(id)).Select(x=>x.Id).ToHashSet();
        if(r.Items.Any(x=>!valid.Contains(x.RecipeId)))throw new ArgumentException("One or more meals do not belong to this outlet.");
        await menu.ReplaceAsync(id,r.Items.Select(x=>new OutletMenuItem {
            Id=Guid.NewGuid(),OutletId=id,RecipeId=x.RecipeId,DayOfWeek=x.DayOfWeek,MealSlot=(MealSlot)x.MealSlot,IsAvailable=x.IsAvailable,DisplayOrder=x.DisplayOrder
        }));
        return await GetMenuAsync();
    }
    public async Task<IReadOnlyList<UserDto>> GetCustomersAsync() {
        if(current.OutletId is not Guid id)return[];
        var customerIds=(await subscriptions.GetByOutletAsync(id)).Select(x=>x.CustomerId).ToHashSet();
        return(await users.GetAllAsync()).Where(x=>customerIds.Contains(x.Id)).Select(x=>new UserDto(x.Id,x.Email,x.FirstName,x.LastName,x.Role.ToString(),x.OutletId)).ToList();
    }
    public async Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync()=>current.OutletId is not Guid id?[]:(await subscriptions.GetByOutletAsync(id)).Select(x=>new SubscriptionDto(x.Id,x.CustomerId,x.OutletId,x.MealPlanId,x.PlanName,x.DeliveryMode.ToString(),x.Price,x.DeliveryFee,x.CustomerTransactionFeePercent,x.TransactionFee,x.TotalCharged,x.OutletAmount,x.Frequency,x.MealsPerDay,x.MealsPerWeek,x.Status.ToString(),x.NextDeliveryDate,0,GetPaymentStatus(x),x.DeliveryCity,x.GrossMealAmount,x.SubscriptionDiscountAmount,x.RestaurantTaxableAmount,x.RestaurantGstAmount,x.RestaurantGstRate,x.RestaurantGstMode.ToString(),x.PlatformServiceFee,x.PlatformServiceGst,x.PackageStatus,x.IsOutletCreated,x.OutletDiscountType.ToString(),x.OutletDiscountValue,x.OutletDiscountReason)).ToList();
    private static string GetPaymentStatus(Subscription x)=>x.IsOutletCreated&&x.PackageStatus=="Active"?"Paid":"Pending";
    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync()=>current.OutletId is not Guid id?[]:(await orders.GetByOutletAsync(id)).Select(x=>new OrderDto(x.Id,x.CustomerId,x.OutletId,x.Total,x.Status.ToString(),x.DeliveryDate,x.Address)).ToList();
    public async Task<IReadOnlyList<DeliveryDto>> GetDeliveriesAsync()=>current.OutletId is not Guid id?[]:(await deliveries.GetByOutletAsync(id)).Select(x=>new DeliveryDto(x.Id,x.OrderId,x.OutletId,x.CustomerName,x.Address,x.ScheduledDate,x.MealSlot.ToString(),x.DeliveryFee,x.Status.ToString())).ToList();
    private static RecipeDto Map(Recipe x)=>new(x.Id,x.OutletId,x.Name,x.Calories,x.ProteinGrams,x.CarbsGrams,x.FatGrams,x.Category.ToString(),x.PricePerMeal,x.LargePricePerMeal,x.Description,x.ImageUrl,x.Tags,x.IsActive,
    x.RecipeIngredients.OrderBy(i=>i.Ingredient.Name).Select(i=>new RecipeIngredientDto(i.IngredientId, i.Ingredient.Name, i.Quantity, i.Unit, i.Ingredient.Allergens.Select(a => new AllergenDto(a.AllergenId, a.Allergen.Name)).OrderBy(a => a.Name).ToList())).ToList(),
    x.RecipeAllergens.Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name)).Concat(x.RecipeIngredients.SelectMany(i=>i.Ingredient.Allergens).Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name))).GroupBy(a=>a.Id).Select(g=>g.First()).OrderBy(a=>a.Name).ToList());
}
public sealed class AdminService(IOutletRepository outlets,IUserRepository users,IPlatformTransactionRepository transactions) : IAdminService
{
    public async Task<IReadOnlyList<OutletDto>> GetOutletsAsync()=>(await outlets.GetAllAsync()).Select(x=>new OutletDto(x.Id,x.Name,x.Slug,x.Subdomain,x.City,x.State,x.Pincode,x.Status.ToString(),x.BillingPlan.ToString(),x.LogoUrl??string.Empty,x.HeroImageUrl??string.Empty,(x.HealthHighlights??string.Empty).Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList(),x.PrimaryColor,x.Status==OutletStatus.Active,0,x.Rating,x.ReviewCount,x.About)).ToList();
    public async Task<IReadOnlyList<UserDto>> GetUsersAsync()=>(await users.GetAllAsync()).Select(x=>new UserDto(x.Id,x.Email,x.FirstName,x.LastName,x.Role.ToString(),x.OutletId)).ToList();
    public async Task<object> GetDashboardAsync()=>new {
        outlets=(await outlets.GetAllAsync()).Count,
        users=(await users.GetAllAsync()).Count,
        revenue=(await GetRevenueAsync()).TotalRevenue
    };
    public async Task<PlatformRevenueDto> GetRevenueAsync() {
        var tx=await transactions.GetAllAsync();
        var outlet=tx.Where(x=>x.Type=="OutletSubscription").Sum(x=>x.GrossAmount);
        var service=tx.Where(x=>x.Type=="CustomerSubscription").Sum(x=>x.PlatformFee);
        var late=tx.Where(x=>x.Type=="LateSkipFee").Sum(x=>x.GrossAmount);
        var commission=tx.Where(x=>x.Type=="OutletCommission").Sum(x=>x.GrossAmount);
        return new(outlet,service,outlet+service+late+commission,late,service,commission);
    }
}
