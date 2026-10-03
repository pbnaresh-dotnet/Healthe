using HealthApp.Application.Abstractions;
using HealthApp.Application.Events;
using HealthApp.Application.Orchestration;
using HealthApp.Application.Strategies;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Domain.Events;
using HealthApp.Shared.DTOs;
namespace HealthApp.Application.Services;
public sealed class AuthService(IUserRepository users, IOutletRepository outlets, ITokenService tokens, IPasswordService passwords) : IAuthService
{
    public async Task<AuthResponse?> LoginAsync(LoginRequest r)
    {
        var user = await users.FindByEmailAsync(r.Email);
        return user is null || !user.IsActive || !passwords.Verify(r.Password, user.PasswordHash) ? null : tokens.CreateToken(user);
    }
    public async Task<AuthResponse> RegisterAsync(RegisterRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 6) throw new ArgumentException("Password must be at least 6 characters.");
        if (await users.FindByEmailAsync(r.Email) is not null) throw new InvalidOperationException("Email is already registered.");
        var role = Enum.TryParse<UserRole>(r.Role, true, out var parsed) ? parsed : UserRole.Customer;
        if (role == UserRole.SuperAdmin) throw new UnauthorizedAccessException("SuperAdmin accounts cannot be self-registered.");
        Guid? outletId = null;
        if (role == UserRole.OutletAdmin)
        {
            if (string.IsNullOrWhiteSpace(r.OutletSlug)) throw new ArgumentException("OutletSlug is required for an outlet admin.");
            outletId = (await outlets.GetBySlugAsync(r.OutletSlug))?.Id ?? throw new KeyNotFoundException("Outlet not found.");
        }
        var user = new User {
            Id = Guid.NewGuid(),
            Email = r.Email.Trim().ToLowerInvariant(),
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            Role = role,
            OutletId = outletId,
            PasswordHash = passwords.Hash(r.Password)
        };
        await users.AddAsync(user);
        return tokens.CreateToken(user);
    }
}
public sealed class MarketplaceService(IOutletRepository outlets, IMealPlanRepository plans, IRecipeRepository recipes, IOutletMenuRepository menu, ISaaSPlanRepository saasPlans) : IMarketplaceService
{
    public async Task<IReadOnlyList<SaaSPlanDto>> GetSaaSPlansAsync() => (await saasPlans.GetActiveAsync()).Select(Map).ToList();
    public async Task<AvailabilityResponse> GetAvailabilityAsync(double latitude, double longitude)
    {
        var result = (await outlets.GetAllAsync()).Where(x => x.Status == OutletStatus.Active)
        .Select(x => (outlet: x, distance: Distance(latitude, longitude, x.Latitude, x.Longitude)))
        .Where(x => x.distance <= x.outlet.ServiceRadiusKm)
        .Select(x => ToDto(x.outlet, x.distance)).ToList();
        return new(result.Count > 0, result.Count > 0 ? $"{result.Count} outlet(s) serve your location." : "No active outlet currently serves your location.", result);
    }
    public async Task<IReadOnlyList<OutletDto>> GetAllOutletsAsync() => (await outlets.GetAllAsync()).Where(x => x.Status == OutletStatus.Active).Select(x => ToDto(x, 0)).ToList();
    public async Task<OutletDto?> GetOutletAsync(string slug) {
        var x = await outlets.GetBySlugAsync(slug);
        return x is null ? null : ToDto(x, 0);
    }
    public async Task<IReadOnlyList<MealPlanDto>> GetPlansAsync(Guid outletId) => (await plans.GetByOutletAsync(outletId)).Where(x => x.IsActive).Select(Map).ToList();
    public async Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(Guid outletId, string? category) => (await recipes.GetByOutletAndCategoryAsync(outletId, category)).Where(x => x.IsActive).Select(Map).ToList();
    public async Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(Guid outletId) => await MapMenu(outletId, await menu.GetByOutletAsync(outletId));
    private async Task<IReadOnlyList<MenuItemDto>> MapMenu(Guid outletId, IReadOnlyList<OutletMenuItem> items)
    {
        var rs = (await recipes.GetByOutletAsync(outletId)).ToDictionary(x => x.Id);
        return items.Where(x => x.IsAvailable).Select(x => rs.TryGetValue(x.RecipeId, out var r) ? new MenuItemDto(x.Id, x.OutletId, x.RecipeId, r.Name, x.DayOfWeek, x.MealSlot.ToString(), (int)x.MealSlot, r.PricePerMeal, r.LargePricePerMeal, r.Calories, r.ProteinGrams, r.Category.ToString(), r.ImageUrl, x.IsAvailable, x.DisplayOrder) : null).Where(x => x is not null).Cast<MenuItemDto>().ToList();
    }
    private static SaaSPlanDto Map(SaaSPlan x) => new(x.Id, x.Name, x.MonthlyFee, x.AnnualFee, x.IncludedActiveCustomers, x.AdditionalCustomerFee, x.CustomerTransactionFeePercent, x.Description, x.IsActive);
    private static MealPlanDto Map(MealPlan x) => new(x.Id, x.OutletId, x.Name, x.Frequency, x.MealsPerDay, x.MealsPerWeek, x.Price, x.Currency, x.Description, x.IsActive);
    private static RecipeDto Map(Recipe x) => new(x.Id,x.OutletId,x.Name,x.Calories,x.ProteinGrams,x.CarbsGrams,x.FatGrams,x.Category.ToString(),x.PricePerMeal,x.LargePricePerMeal,x.Description,x.ImageUrl,x.Tags,x.IsActive,
    x.RecipeIngredients.OrderBy(i=>i.Ingredient.Name).Select(i=>new RecipeIngredientDto(i.IngredientId,i.Ingredient.Name,i.Quantity,i.Unit,i.Ingredient.Allergens.Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name)).OrderBy(a=>a.Name).ToList())).ToList(),
    x.RecipeAllergens.Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name)).Concat(x.RecipeIngredients.SelectMany(i=>i.Ingredient.Allergens).Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name))).GroupBy(a=>a.Id).Select(g=>g.First()).OrderBy(a=>a.Name).ToList());
    private static OutletDto ToDto(Outlet x, double distance) => new(x.Id, x.Name, x.Slug, x.Subdomain, x.City, x.State, x.Pincode, x.Status.ToString(), x.BillingPlan.ToString(), x.LogoUrl, x.PrimaryColor, true, Math.Round(distance, 1));
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
IDeliveryCalculator deliveryCalculator, IDiscountCodeRepository discountCodes, IOrderFinancialRepository orderFinancials, IDeliveryRepository deliveries, IAllergySafetyService allergySafety) : ICustomerService
{
    public async Task<UserDto?> GetProfileAsync()
    {
        if (current.UserId is not Guid id) return null;
        var x = await users.FindByIdAsync(id);
        return x is null ? null : new(x.Id, x.Email, x.FirstName, x.LastName, x.Role.ToString(), x.OutletId);
    }
    public async Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync()
    {
        if (current.UserId is not Guid id) return [];
        var result = new List<SubscriptionDto>();
        foreach (var x in await subs.GetByCustomerAsync(id)) result.Add(await ToDto(x));
        return result;
    }
    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync() => current.UserId is not Guid id ? [] : (await orders.GetByCustomerAsync(id)).Select(x => new OrderDto(x.Id,x.CustomerId,x.OutletId,x.Total,x.Status.ToString(),x.DeliveryDate,x.Address)).ToList();
    public async Task<SubscriptionQuoteDto?> QuoteAsync(SubscriptionQuoteRequest r)
    {
        if (current.UserId is not Guid customerId) return null;
        var deliveryMode = Parse<SubscriptionDeliveryMode>(r.DeliveryMode, "delivery mode");
        var duration = Parse<SubscriptionDuration>(r.Duration, "duration");
        var outlet = await outlets.GetByIdAsync(r.OutletId) ?? throw new KeyNotFoundException("Outlet not found.");
        if (outlet.Status != OutletStatus.Active) throw new InvalidOperationException("Outlet is not active.");
        var rs = (await recipes.GetByOutletAsync(outlet.Id)).Where(x => x.IsActive).ToDictionary(x => x.Id);
        var menuItems = await menu.GetByOutletAsync(outlet.Id);
        var meals = BuildSelections(r.Selections, outlet.Id, menuItems, rs);
        ValidateDeliveryMode(deliveryMode, meals);
        ValidateSelectionWindow(duration, meals);
        var selectedRecipes = meals.Select(x=>rs[x.RecipeId]).DistinctBy(x=>x.Id).ToList();
        var allergyWarnings = await allergySafety.GetWarningsAsync(customerId, selectedRecipes);
        foreach (var m in meals) if (!m.AddressId.HasValue) throw new ArgumentException("Every scheduled meal requires a delivery address.");
        var tiers = await discountTiers.GetByOutletAsync(outlet.Id);
        var packageDiscount = discountStrategy.Calculate(new(duration, meals), tiers);
        var gross = Math.Round(meals.Sum(x => x.MealPrice), 2);
        var codeAmount = await CalculateDiscountCodeAmountAsync(outlet.Id, gross, r.DiscountCode);
        var totalDiscount = Math.Min(gross, packageDiscount.Amount + codeAmount);
        var net = Math.Round(gross - totalDiscount, 2);
        var delivery = await CalculateDeliveryAsync(outlet.Id, deliveryMode, meals, customerId);
        var service = platformFee.Calculate(net);
        var taxes = taxStrategy.Calculate(net, service);
        var commissionRate = await GetOutletCommissionAsync(outlet.Id);
        var commission = Math.Round(net * commissionRate, 2);
        var quotes = new List<DeliveryQuoteDto>();
        foreach (var addressId in meals.Select(x => x.AddressId!.Value).Distinct()) quotes.Add(await deliveryCalculator.QuoteAsync(outlet.Id, customerId, addressId));
        var payable = net + taxes.RestaurantAmount + delivery + service + taxes.PlatformAmount;
        return new(gross, gross == 0 ? 0 : Math.Round(totalDiscount / gross * 100m, 4), totalDiscount, net, taxes.RestaurantAmount, delivery, service, taxes.PlatformAmount, payable, commissionRate, commission, service + commission, quotes, allergyWarnings, allergyWarnings.Count>0 && !allergyWarnings.All(x=>(r.ConfirmedAllergyRecipeIds??[]).Contains(x.RecipeId)));
    }
    public async Task<SubscriptionDto?> SubscribeAsync(CreateSubscriptionRequest r)
    {
        if (current.UserId is not Guid customerId) return null;
        var deliveryMode = Parse<SubscriptionDeliveryMode>(r.DeliveryMode, "delivery mode");
        _ = deliveryModeFactory.Create(deliveryMode);
        var duration = Parse<SubscriptionDuration>(r.Duration, "package duration");
        if (r.Selections is null || r.Selections.Count == 0) throw new ArgumentException("Add at least one meal to your package.");
        var outlet = await outlets.GetByIdAsync(r.OutletId) ?? throw new KeyNotFoundException("Outlet not found or unavailable.");
        if (outlet.Status != OutletStatus.Active) throw new KeyNotFoundException("Outlet not found or unavailable.");
        var rs = (await recipes.GetByOutletAsync(outlet.Id)).Where(x => x.IsActive).ToDictionary(x => x.Id);
        var mealEntities = BuildSelections(r.Selections, outlet.Id, await menu.GetByOutletAsync(outlet.Id), rs);
        ValidateDeliveryMode(deliveryMode, mealEntities);
        ValidateSelectionWindow(duration, mealEntities);
        var selectedRecipes = mealEntities.Select(x=>rs[x.RecipeId]).DistinctBy(x=>x.Id).ToList();
        await allergySafety.EnsureConfirmedAsync(customerId, selectedRecipes, r.ConfirmedAllergyRecipeIds);
        foreach (var meal in mealEntities)
        {
            if (!meal.AddressId.HasValue) throw new ArgumentException("Select a delivery address for every meal.");
            _ = await deliveryCalculator.QuoteAsync(outlet.Id, customerId, meal.AddressId.Value);
        }
        var discount = discountStrategy.Calculate(new(duration, mealEntities), await discountTiers.GetByOutletAsync(outlet.Id));
        var gross = Math.Round(mealEntities.Sum(x => x.MealPrice), 2);
        var discountCodeResult = await CalculateDiscountCodeAsync(outlet.Id, gross, r.DiscountCode);
        var totalDiscount = Math.Min(gross, discount.Amount + discountCodeResult.Amount);
        var net = Math.Round(gross - totalDiscount, 2);
        var delivery = await CalculateDeliveryAsync(outlet.Id, deliveryMode, mealEntities, customerId);
        var serviceFee = platformFee.Calculate(net);
        var taxes = taxStrategy.Calculate(net, serviceFee);
        var commissionRate = await GetOutletCommissionAsync(outlet.Id);
        var commission = Math.Round(net * commissionRate, 2);
        var start = mealEntities.Min(x => x.MealDate).Date;
        var end = duration switch {
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
                Id=Guid.NewGuid(),OrderId=order.Id,GrossMealAmount=gross,DiscountAmount=totalDiscount,NetMealAmount=net,DeliveryAmount=delivery,PlatformServiceFee=serviceFee,PlatformServiceGst=taxes.PlatformAmount,RestaurantGstAmount=taxes.RestaurantAmount,LateSkipFee=0,CustomerPayable=subscription.TotalCharged,OutletCommission=commission,OutletCommissionGst=0,OutletSettlementAmount=subscription.OutletAmount,HealthAppRevenue=serviceFee+commission
            });
            var customer = await users.FindByIdAsync(customerId);
            var groups = deliveryMode == SubscriptionDeliveryMode.OneDeliveryPerDay ? mealEntities.GroupBy(x=>x.MealDate.Date).Select(g=>(IEnumerable<SubscriptionMealSelection>)g) : mealEntities.Select(x=>(IEnumerable<SubscriptionMealSelection>)new[] {
                x
            });
            foreach (var group in groups)
            {
                var first = group.First();  var address = await addresses.GetAsync(customerId, first.AddressId!.Value) ?? throw new InvalidOperationException("Delivery address could not be resolved.");
                await deliveries.AddAsync(new Delivery {
                    Id=Guid.NewGuid(),OrderId=order.Id,SubscriptionId=subscription.Id,OutletId=outlet.Id,CustomerId=customerId,DeliveryAddressId=address.Id,ScheduledDate=first.MealDate,MealSlot=first.MealSlot,CustomerName=customer is null?"":$"{customer.FirstName} {customer.LastName}".Trim(),Address=$"{address.AddressLine1}, {address.AddressLine2}, {address.ContactPhone}".Trim(' ',','),DeliveryFee=first.DeliveryFee,Status=DeliveryStatus.Scheduled
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
                Id=Guid.NewGuid(),OrderId=existing.FirstOrDefault()?.OrderId??Guid.Empty,SubscriptionId=s.Id,OutletId=s.OutletId,CustomerId=s.CustomerId,DeliveryAddressId=address.Id,ScheduledDate=newDate,MealSlot=(MealSlot)r.NewMealSlot,CustomerName=customer is null?"":$"{customer.FirstName} {customer.LastName}".Trim(),Address=$"{address.AddressLine1}, {address.AddressLine2}, {address.ContactPhone}".Trim(' ',','),DeliveryFee=q.DeliveryFee,Status=DeliveryStatus.Scheduled
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
    private async Task<Subscription> GetOwnedSubscription(Guid id) {
        var s=await subs.GetAsync(id)??throw new KeyNotFoundException("Subscription not found.");
        if(current.UserId is not Guid uid||s.CustomerId!=uid)throw new UnauthorizedAccessException("Subscription does not belong to the current customer.");
        return s;
    }
    private async Task<SubscriptionDto> ToDto(Subscription x)=>new(x.Id,x.CustomerId,x.OutletId,x.MealPlanId,x.PlanName,x.DeliveryMode.ToString(),x.Price,x.DeliveryFee,x.CustomerTransactionFeePercent,x.TransactionFee,x.TotalCharged,x.OutletAmount,x.Frequency,x.MealsPerDay,x.MealsPerWeek,x.Status.ToString(),x.NextDeliveryDate,await credits.GetBalanceAsync(x.CustomerId));
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
        return(await menu.GetByOutletAsync(outletId)).Where(x=>x.IsAvailable).Select(x=>rs.TryGetValue(x.RecipeId,out var r)?new MenuItemDto(x.Id,x.OutletId,x.RecipeId,r.Name,x.DayOfWeek,x.MealSlot.ToString(),(int)x.MealSlot,r.PricePerMeal,r.LargePricePerMeal,r.Calories,r.ProteinGrams,r.Category.ToString(),r.ImageUrl,x.IsAvailable,x.DisplayOrder):null).Where(x=>x is not null).Cast<MenuItemDto>().ToList();
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
            SubscriptionDuration.OneWeek=>start.AddDays(6),
            SubscriptionDuration.TwoWeeks=>start.AddDays(13),
            SubscriptionDuration.OneMonth=>start.AddDays(27),
            _=>start
        };
        if(meals.Any(x=>x.MealDate.Date<start||x.MealDate.Date>end))throw new ArgumentException("Selected meals are outside the package duration.");
        foreach(var g in meals.GroupBy(x=>(x.MealDate.Date-start).Days/7))if(g.Select(x=>x.MealDate.Date).Distinct().Count()>7)throw new ArgumentException("A package can contain at most seven active days in a week.");
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
    public async Task<object> GetDashboardAsync()
    {
        if(current.OutletId is not Guid id)
            throw new UnauthorizedAccessException("The current user is not associated with an outlet.");

        var outlet=await GetCurrentAsync()??throw new KeyNotFoundException("Outlet not found.");
        var subscriptionsList=await subscriptions.GetByOutletAsync(id);
        var plansList=await plans.GetByOutletAsync(id);
        var recipesList=await recipes.GetByOutletAsync(id);
        var customersList=await users.GetAllAsync();
        var ordersList=await orders.GetByOutletAsync(id);
        var customerIds=subscriptionsList.Select(x=>x.CustomerId).ToHashSet();
        var customerMap=customersList.Where(x=>customerIds.Contains(x.Id)).ToDictionary(x=>x.Id);
        var windowStart=DateTime.UtcNow.Date.AddDays(-6);
        var newSubscriptions=subscriptionsList.Where(x=>x.StartDate.Date>=windowStart).ToList();
        var newCustomers=subscriptionsList
            .GroupBy(x=>x.CustomerId)
            .Count(g=>g.Min(x=>x.StartDate).Date>=windowStart);
        var recentSubscriptions=newSubscriptions
            .OrderByDescending(x=>x.StartDate)
            .Take(8)
            .Select(x=>new OutletDashboardSubscriptionDto(
                x.Id,
                x.CustomerId,
                customerMap.TryGetValue(x.CustomerId,out var customer)
                    ? $"{customer.FirstName} {customer.LastName}".Trim()
                    : "Customer",
                x.PlanName,
                x.StartDate,
                x.EndDate,
                x.MealsPerWeek,
                x.Status.ToString()))
            .ToList();
        var todayLabels=await deliveryLabels.GetLabelsAsync(DateTime.UtcNow.Date);

        return new
        {
            outlet,
            mealPlans=plansList.Count(x=>x.IsActive),
            recipes=recipesList.Count(x=>x.IsActive),
            customers=customerIds.Count,
            subscriptions=subscriptionsList.Count,
            orders=ordersList.Count,
            newCustomers,
            newSubscriptions=newSubscriptions.Count,
            todayMeals=todayLabels.Count,
            recentSubscriptions
        };
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
            var area=address is null ? null : await areas.GetAsync(address.CityAreaId);
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
            subscription.NetMealAmount,
            subscription.SubscriptionDiscountAmount,
            subscription.RestaurantGstAmount,
            Math.Round(subscription.NetMealAmount+subscription.RestaurantGstAmount,2),
            subscription.DeliveryFee,
            subscription.Status.ToString(),
            meals);
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
        return x is null?null:new(x.Id,x.Name,x.Slug,x.Subdomain,x.City,x.State,x.Pincode,x.Status.ToString(),x.BillingPlan.ToString(),x.LogoUrl,x.PrimaryColor,true,0);
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
    public async Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync()=>current.OutletId is not Guid id?[]:(await subscriptions.GetByOutletAsync(id)).Select(x=>new SubscriptionDto(x.Id,x.CustomerId,x.OutletId,x.MealPlanId,x.PlanName,x.DeliveryMode.ToString(),x.Price,x.DeliveryFee,x.CustomerTransactionFeePercent,x.TransactionFee,x.TotalCharged,x.OutletAmount,x.Frequency,x.MealsPerDay,x.MealsPerWeek,x.Status.ToString(),x.NextDeliveryDate,0)).ToList();
    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync()=>current.OutletId is not Guid id?[]:(await orders.GetByOutletAsync(id)).Select(x=>new OrderDto(x.Id,x.CustomerId,x.OutletId,x.Total,x.Status.ToString(),x.DeliveryDate,x.Address)).ToList();
    public async Task<IReadOnlyList<DeliveryDto>> GetDeliveriesAsync()=>current.OutletId is not Guid id?[]:(await deliveries.GetByOutletAsync(id)).Select(x=>new DeliveryDto(x.Id,x.OrderId,x.OutletId,x.CustomerName,x.Address,x.ScheduledDate,x.MealSlot.ToString(),x.DeliveryFee,x.Status.ToString())).ToList();
    private static RecipeDto Map(Recipe x)=>new(x.Id,x.OutletId,x.Name,x.Calories,x.ProteinGrams,x.CarbsGrams,x.FatGrams,x.Category.ToString(),x.PricePerMeal,x.LargePricePerMeal,x.Description,x.ImageUrl,x.Tags,x.IsActive,
    x.RecipeIngredients.OrderBy(i=>i.Ingredient.Name).Select(i=>new RecipeIngredientDto(i.IngredientId, i.Ingredient.Name, i.Quantity, i.Unit, i.Ingredient.Allergens.Select(a => new AllergenDto(a.AllergenId, a.Allergen.Name)).OrderBy(a => a.Name).ToList())).ToList(),
    x.RecipeAllergens.Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name)).Concat(x.RecipeIngredients.SelectMany(i=>i.Ingredient.Allergens).Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name))).GroupBy(a=>a.Id).Select(g=>g.First()).OrderBy(a=>a.Name).ToList());
}
public sealed class AdminService(IOutletRepository outlets,IUserRepository users,IPlatformTransactionRepository transactions) : IAdminService
{
    public async Task<IReadOnlyList<OutletDto>> GetOutletsAsync()=>(await outlets.GetAllAsync()).Select(x=>new OutletDto(x.Id,x.Name,x.Slug,x.Subdomain,x.City,x.State,x.Pincode,x.Status.ToString(),x.BillingPlan.ToString(),x.LogoUrl,x.PrimaryColor,x.Status==OutletStatus.Active,0)).ToList();
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
