using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class CustomerProfileService(ICurrentUser current, ICustomerProfileRepository profiles, IAllergenRepository allergens, ICustomerAllergyRepository customerAllergies) : ICustomerProfileService
{
    public async Task<CustomerProfileDto?> GetAsync()
    {
        if(current.UserId is not Guid id)return null;
        var p=await profiles.GetAsync(id);
        return p is null?null:await Map(p,id);
    }
    public async Task<CustomerProfileDto?> SaveAsync(SaveCustomerProfileRequest r)
    {
        if(current.UserId is not Guid id)return null;
        if(r.WeightKg is <=0||r.HeightCm is <=0)throw new ArgumentException("Weight and height must be positive.");
        var requested=(r.AllergyIds??[]).Distinct().ToList();
        var valid=await allergens.GetByIdsAsync(requested);
        if(valid.Count!=requested.Count)throw new ArgumentException("One or more selected allergies are invalid.");
        decimal? bmi=r.WeightKg.HasValue&&r.HeightCm.HasValue?Math.Round(r.WeightKg.Value/((r.HeightCm.Value/100m)*(r.HeightCm.Value/100m)),2):null;
        var p=await profiles.GetAsync(id)??new CustomerProfile{Id=Guid.NewGuid(),CustomerId=id};
        p.WeightKg=r.WeightKg;p.HeightCm=r.HeightCm;p.Bmi=bmi;p.DateOfBirth=r.DateOfBirth;p.Goal=r.Goal;p.ActivityLevel=r.ActivityLevel;p.Diet=r.Diet;p.UpdatedAtUtc=DateTime.UtcNow;
        await profiles.AddOrUpdateAsync(p);
        await customerAllergies.ReplaceAsync(id,requested);
        p=await profiles.GetAsync(id)??p;
        return await Map(p,id);
    }
    private async Task<CustomerProfileDto> Map(CustomerProfile p,Guid customerId)
    {
        var allergies=await customerAllergies.GetByCustomerAsync(customerId);
        return new(
            p.Id,
            p.CustomerId,
            p.WeightKg,
            p.HeightCm,
            p.Bmi,
            p.Goal,
            p.ActivityLevel,
            p.Diet,
            p.UpdatedAtUtc,
            allergies.Select(a=>new AllergenDto(a.AllergenId,a.Allergen.Name)).OrderBy(a=>a.Name).ToList());
    }
}

public sealed class CustomerAddressService(ICurrentUser current,ICustomerAddressRepository addresses,ICityAreaRepository areas,IGeocodingService geocoding,IDeliveryCalculator calculator) : ICustomerAddressService
{
    public async Task<IReadOnlyList<CustomerAddressDto>> GetAsync()=>current.UserId is not Guid id?[]:await Map(await addresses.GetByCustomerAsync(id));
    public async Task<CustomerAddressDto?> CreateAsync(CreateCustomerAddressRequest r){if(current.UserId is not Guid id)return null;var area=await ValidateAreaAndPinAsync(r.CityAreaId,r.Latitude,r.Longitude);if(string.IsNullOrWhiteSpace(r.AddressLine1))throw new ArgumentException("Address line 1 is required.");var a=new CustomerAddress{Id=Guid.NewGuid(),CustomerId=id,CityAreaId=area.Id,Label=r.Label?.Trim()??"",AddressLine1=r.AddressLine1.Trim(),AddressLine2=r.AddressLine2?.Trim()??"",ContactName=r.ContactName?.Trim()??"",ContactPhone=r.ContactPhone?.Trim()??"",Latitude=r.Latitude,Longitude=r.Longitude,IsDefault=r.IsDefault};await addresses.AddAsync(a);return(await Map(new[]{a})).First();}
    public async Task<CustomerAddressDto?> UpdateAsync(Guid addressId,UpdateCustomerAddressRequest r){if(current.UserId is not Guid id)return null;var a=await addresses.GetAsync(id,addressId);if(a is null)return null;var area=await ValidateAreaAndPinAsync(r.CityAreaId,r.Latitude,r.Longitude);if(string.IsNullOrWhiteSpace(r.AddressLine1))throw new ArgumentException("Address line 1 is required.");a.CityAreaId=area.Id;a.Label=r.Label?.Trim()??"";a.AddressLine1=r.AddressLine1.Trim();a.AddressLine2=r.AddressLine2?.Trim()??"";a.ContactName=r.ContactName?.Trim()??"";a.ContactPhone=r.ContactPhone?.Trim()??"";a.Latitude=r.Latitude;a.Longitude=r.Longitude;a.IsDefault=r.IsDefault;await addresses.UpdateAsync(a);return(await Map(new[]{a})).First();}
    public async Task<bool> DeleteAsync(Guid id){if(current.UserId is not Guid uid)return false;var a=await addresses.GetAsync(uid,id);if(a is null)return false;await addresses.DeleteAsync(uid,id);return true;}
    private static void ValidateCoordinates(double latitude,double longitude){if(double.IsNaN(latitude)||double.IsInfinity(latitude)||latitude is < -90 or > 90)throw new ArgumentException("Latitude must be between -90 and 90.");if(double.IsNaN(longitude)||double.IsInfinity(longitude)||longitude is < -180 or > 180)throw new ArgumentException("Longitude must be between -180 and 180.");}
    private async Task<CityArea> ValidateAreaAndPinAsync(Guid cityAreaId,double latitude,double longitude)
    {
        ValidateCoordinates(latitude,longitude);
        var area=await areas.GetAsync(cityAreaId)??throw new KeyNotFoundException("Delivery area not found.");
        if(!area.IsActive)throw new ArgumentException("The selected delivery area is inactive.");
        var resolved=await geocoding.ReverseAsync(latitude,longitude);
        if(resolved is null||string.IsNullOrWhiteSpace(resolved.City))
            throw new ArgumentException("We could not determine the city for this map pin. Move the pin onto a supported delivery location and try again.");
        if(!resolved.City.Equals(area.City,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The map pin resolves to {resolved.City}, but the selected delivery area is in {area.City}. Select a pin inside {area.City}.");

        // The map pin is authoritative. When reverse geocoding provides a postcode,
        // require the selected HealthApp delivery area to use the same postcode.
        // This prevents saving an arbitrary area from the same city while the pin
        // actually belongs to another configured delivery area.
        if(!string.IsNullOrWhiteSpace(resolved.Pincode) &&
           !string.IsNullOrWhiteSpace(area.Pincode) &&
           !resolved.Pincode.Equals(area.Pincode,StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The map pin is in postcode {resolved.Pincode}, but {area.Name} uses postcode {area.Pincode}. Select the delivery area matching the pinned location.");
        }

        return area;
    }
    public async Task<IReadOnlyList<DeliveryQuoteDto>> QuoteAsync(Guid outletId){if(current.UserId is not Guid id)return[];var result=new List<DeliveryQuoteDto>();foreach(var a in await addresses.GetByCustomerAsync(id)){try{result.Add(await calculator.QuoteAsync(outletId,id,a.Id));}catch{}}return result;}
    private async Task<IReadOnlyList<CustomerAddressDto>> Map(IEnumerable<CustomerAddress> rows){var result=new List<CustomerAddressDto>();foreach(var a in rows){var area=await areas.GetAsync(a.CityAreaId);result.Add(new(a.Id,a.Label,area?.Name??"",area?.City??"",area?.Pincode??"",a.AddressLine1,a.AddressLine2,a.ContactName,a.ContactPhone,a.Latitude,a.Longitude,a.IsDefault));}return result;}
}

public sealed class OutletDeliveryService(ICurrentUser current,ICityAreaRepository cityAreas,IOutletDeliveryAreaRepository outletAreas,IDeliveryPricingRepository pricing,IOutletRepository outlets) : IOutletDeliveryService
{
    public async Task<IReadOnlyList<CityAreaDto>> GetAvailableAreasAsync(string? city){if(current.OutletId is not Guid id)return[];var outlet=await outlets.GetByIdAsync(id)??throw new KeyNotFoundException("Outlet not found.");var requestedCity=string.IsNullOrWhiteSpace(city)?outlet.City:city.Trim();if(!requestedCity.Equals(outlet.City,StringComparison.OrdinalIgnoreCase))return[];return(await cityAreas.GetActiveAsync(outlet.City)).Select(x=>new CityAreaDto(x.Id,x.City,x.State,x.Name,x.Pincode,x.Latitude,x.Longitude,x.IsActive)).ToList();}
    public async Task<IReadOnlyList<OutletDeliveryAreaDto>> GetAreasAsync(){if(current.OutletId is not Guid id)return[];var rows=await outletAreas.GetByOutletAsync(id);var areas=await cityAreas.GetActiveAsync();return rows.Select(x=>{var a=areas.FirstOrDefault(y=>y.Id==x.CityAreaId);return new OutletDeliveryAreaDto(x.Id,x.OutletId,x.CityAreaId,a?.Name??"",a?.City??"",a?.Pincode??"",x.IsActive);}).ToList();}
    public async Task<IReadOnlyList<DeliveryPricingRuleDto>> GetPricingAsync()=>current.OutletId is not Guid id?[]:(await pricing.GetByOutletAsync(id)).Select(x=>new DeliveryPricingRuleDto(x.Id,x.OutletId,x.MaxDistanceKm,x.Fee,x.IsActive)).ToList();
    public async Task<IReadOnlyList<OutletDeliveryAreaDto>> SaveAreasAsync(SaveOutletDeliveryAreasRequest r){if(current.OutletId is not Guid id)return[];var outlet=await outlets.GetByIdAsync(id)??throw new KeyNotFoundException("Outlet not found.");var all=await cityAreas.GetActiveAsync(outlet.City);var ids=r.CityAreaIds?.Distinct().ToList()??[];if(ids.Except(all.Select(x=>x.Id)).Any())throw new ArgumentException("One or more areas are invalid or belong to another city.");await outletAreas.ReplaceAsync(id,ids.Select(x=>new OutletDeliveryArea{Id=Guid.NewGuid(),OutletId=id,CityAreaId=x,IsActive=true}));return await GetAreasAsync();}
    public async Task<DeliveryPricingRuleDto?> AddPricingAsync(CreateDeliveryPricingRuleRequest r){if(current.OutletId is not Guid id)return null;if(r.MaxDistanceKm<=0||r.Fee<0)throw new ArgumentException("Distance and fee must be valid.");var existing=await pricing.GetByOutletAsync(id);if(existing.Any(x=>x.MaxDistanceKm==r.MaxDistanceKm))throw new ArgumentException($"A delivery pricing slab for {r.MaxDistanceKm:0.##} km already exists.");var x=new DeliveryPricingRule{Id=Guid.NewGuid(),OutletId=id,MaxDistanceKm=r.MaxDistanceKm,Fee=r.Fee};await pricing.AddAsync(x);return new(x.Id,x.OutletId,x.MaxDistanceKm,x.Fee,x.IsActive);}
    public async Task<bool> DeletePricingAsync(Guid id){if(current.OutletId is not Guid oid)return false;await pricing.DeleteAsync(id,oid);return true;}
}

public sealed class DiscountConfigurationService(ICurrentUser current,ISubscriptionDiscountTierRepository tiers) : IDiscountConfigurationService
{
    public async Task<IReadOnlyList<SubscriptionDiscountTierDto>> GetTiersAsync()=>current.OutletId is not Guid id?[]:(await tiers.GetByOutletAsync(id)).Select(Map).ToList();
    public async Task<SubscriptionDiscountTierDto?> AddTierAsync(SaveSubscriptionDiscountTierRequest r){if(current.OutletId is not Guid id)return null;Validate(r);var x=new SubscriptionDiscountTier{Id=Guid.NewGuid(),OutletId=id,MinMeals=r.MinMeals,MaxMeals=r.MaxMeals,OneWeekPercent=r.OneWeekPercent,TwoWeeksPercent=r.TwoWeeksPercent,OneMonthPercent=r.OneMonthPercent,IsActive=r.IsActive};await tiers.AddAsync(x);return Map(x);}
    public async Task<SubscriptionDiscountTierDto?> UpdateTierAsync(Guid id,SaveSubscriptionDiscountTierRequest r){if(current.OutletId is not Guid oid)return null;var x=(await tiers.GetByOutletAsync(oid)).FirstOrDefault(y=>y.Id==id);if(x is null)return null;Validate(r);x.MinMeals=r.MinMeals;x.MaxMeals=r.MaxMeals;x.OneWeekPercent=r.OneWeekPercent;x.TwoWeeksPercent=r.TwoWeeksPercent;x.OneMonthPercent=r.OneMonthPercent;x.IsActive=r.IsActive;await tiers.UpdateAsync(x);return Map(x);}
    public async Task<bool> DeleteTierAsync(Guid id){if(current.OutletId is not Guid oid)return false;await tiers.DeleteAsync(oid,id);return true;}
    private static void Validate(SaveSubscriptionDiscountTierRequest r){if(r.MinMeals<1||(r.MaxMeals.HasValue&&r.MaxMeals.Value<r.MinMeals)||new[]{r.OneWeekPercent,r.TwoWeeksPercent,r.OneMonthPercent}.Any(x=>x<0||x>100))throw new ArgumentException("Invalid discount tier.");}
    private static SubscriptionDiscountTierDto Map(SubscriptionDiscountTier x)=>new(x.Id,x.OutletId,x.MinMeals,x.MaxMeals,x.OneWeekPercent,x.TwoWeeksPercent,x.OneMonthPercent,x.IsActive);
}

public sealed class CityAreaAdminService(ICityAreaRepository areas) : ICityAreaAdminService
{
    public async Task<IReadOnlyList<CityAreaDto>> GetAsync(string? city)=>(await areas.GetActiveAsync(city)).Select(x=>new CityAreaDto(x.Id,x.City,x.State,x.Name,x.Pincode,x.Latitude,x.Longitude,x.IsActive)).ToList();
    public async Task<CityAreaDto?> CreateAsync(CreateCityAreaRequest r){var x=new CityArea{Id=Guid.NewGuid(),City=r.City,State=r.State,Name=r.Name,Pincode=r.Pincode,Latitude=r.Latitude,Longitude=r.Longitude};await areas.AddAsync(x);return new(x.Id,x.City,x.State,x.Name,x.Pincode,x.Latitude,x.Longitude,x.IsActive);}
}

public sealed class PaymentService(ICurrentUser current,IPaymentTransactionRepository payments,ISubscriptionRepository subscriptions,IOrderRepository orders) : IPaymentService
{
    public async Task<PaymentDto?> CreateAsync(CreatePaymentRequest r){if(current.UserId is not Guid id)return null;if(string.IsNullOrWhiteSpace(r.IdempotencyKey))throw new ArgumentException("Idempotency key is required.");var s=await subscriptions.GetAsync(r.SubscriptionId)??throw new KeyNotFoundException("Subscription not found.");if(s.CustomerId!=id)throw new UnauthorizedAccessException();var existing=await payments.GetByIdempotencyKeyAsync(r.IdempotencyKey);if(existing is not null)return Map(existing);var now=DateTime.UtcNow;var p=new PaymentTransaction{Id=Guid.NewGuid(),CustomerId=id,SubscriptionId=s.Id,Provider=r.Provider,ProviderPaymentId=$"mock_{Guid.NewGuid():N}",IdempotencyKey=r.IdempotencyKey,Amount=s.TotalCharged,Currency="INR",Status="Paid",CreatedAtUtc=now,PaidAtUtc=now};await payments.AddAsync(p);var order=await orders.GetBySubscriptionAsync(s.Id);if(order is not null){order.Status=OrderStatus.Confirmed;await orders.UpdateAsync(order);}return Map(p);}
    public async Task<PaymentDto?> GetAsync(Guid id){if(current.UserId is not Guid uid)return null;var p=await payments.GetAsync(id);return p is null||p.CustomerId!=uid?null:Map(p);}
    private static PaymentDto Map(PaymentTransaction p)=>new(p.Id,p.SubscriptionId,p.Provider,p.ProviderPaymentId,p.Amount,p.Currency,p.Status,p.CreatedAtUtc,p.PaidAtUtc);
}

public sealed class DeliveryLabelService(
    ICurrentUser current,
    IDeliveryRepository deliveries,
    IOutletRepository outlets,
    ISubscriptionRepository subscriptions,
    ISubscriptionMealSelectionRepository selections,
    IRecipeRepository recipes,
    ICustomerAddressRepository addresses,
    ICityAreaRepository areas,
    IUserRepository users) : IDeliveryLabelService
{
    public async Task<IReadOnlyList<DeliveryLabelDto>> GetLabelsAsync(DateTime? date)
    {
        if(current.OutletId is not Guid id)
            return [];

        var outlet=await outlets.GetByIdAsync(id);
        if(outlet is null)
            return [];

        var subscriptionRows=await subscriptions.GetByOutletAsync(id);
        var subscriptionMap=subscriptionRows.ToDictionary(x=>x.Id);
        var rows=await deliveries.GetByOutletAsync(id);

        if(date.HasValue)
            rows=rows.Where(x=>x.ScheduledDate.Date==date.Value.Date).ToList();

        var result=new List<DeliveryLabelDto>();

        foreach(var delivery in rows.Where(x=>x.Status!=DeliveryStatus.Skipped))
        {
            if(!subscriptionMap.TryGetValue(delivery.SubscriptionId,out var subscription))
                continue;

            var selectionsForDay=await selections.GetBySubscriptionAndDateRangeAsync(
                delivery.SubscriptionId,
                delivery.ScheduledDate.Date,
                delivery.ScheduledDate.Date.AddDays(1));

            var mealRows=selectionsForDay
                .Where(x=>x.Status==MealSelectionStatus.Scheduled||
                          x.Status==MealSelectionStatus.Prepared||
                          x.Status==MealSelectionStatus.OutForDelivery)
                .ToList();

            if(subscription.DeliveryMode!=SubscriptionDeliveryMode.OneDeliveryPerDay)
            {
                mealRows=mealRows
                    .Where(x=>x.MealSlot==delivery.MealSlot&&
                              x.AddressId==delivery.DeliveryAddressId)
                    .ToList();
            }

            var address=delivery.DeliveryAddressId.HasValue
                ? await addresses.GetAsync(delivery.CustomerId,delivery.DeliveryAddressId.Value)
                : null;
            var area=address is null ? null : await areas.GetAsync(address.CityAreaId);
            var customer=await users.FindByIdAsync(delivery.CustomerId);

            foreach(var meal in mealRows)
            {
                var recipe=await recipes.GetAsync(meal.RecipeId);
                var slot=GetMealSlotInfo(delivery.MealSlot);

                result.Add(new DeliveryLabelDto(
                    subscription.Id,
                    meal.Id,
                    meal.MealDate,
                    (int)delivery.MealSlot,
                    slot.Name,
                    slot.Window,
                    outlet.Name,
                    outlet.LogoUrl,
                    customer is null
                        ? delivery.CustomerName
                        : $"{customer.FirstName} {customer.LastName}".Trim(),
                    address?.ContactPhone??"",
                    recipe?.Name??"Meal",
                    recipe?.Category.ToString()??"",
                    meal.PortionSize.ToString(),
                    subscription.PlanName,
                    address?.Label??"",
                    address is null
                        ? delivery.Address
                        : $"{address.AddressLine1}, {address.AddressLine2}".Trim(' ',','),
                    area?.Name??"",
                    area?.Pincode??"",
                    meal.MealPrice,
                    delivery.DeliveryFee));
            }
        }

        return result
            .OrderBy(x=>x.MealDate)
            .ThenBy(x=>x.MealSlot)
            .ThenBy(x=>x.CustomerName)
            .ThenBy(x=>x.MealName)
            .ToList();
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
}

public sealed class OutletDiscountCodeService(ICurrentUser current,IDiscountCodeRepository codes) : IOutletDiscountCodeService
{
    public async Task<IReadOnlyList<DiscountCodeDto>> GetAsync()=>current.OutletId is not Guid id?[]:(await codes.GetByOutletAsync(id)).Select(Map).ToList();
    public async Task<DiscountCodeDto?> CreateAsync(CreateDiscountCodeRequest r){if(current.OutletId is not Guid id)return null;if(string.IsNullOrWhiteSpace(r.Code)||r.Percent<=0||r.Percent>100)throw new ArgumentException("Invalid discount code.");var x=new DiscountCode{Id=Guid.NewGuid(),OutletId=id,Code=r.Code.Trim().ToUpperInvariant(),Percent=r.Percent,MaxAmount=r.MaxAmount,MaxRedemptions=r.MaxRedemptions,StartsAtUtc=r.StartsAtUtc,EndsAtUtc=r.EndsAtUtc,IsActive=r.IsActive};await codes.AddAsync(x);return Map(x);}
    public async Task<bool> DisableAsync(Guid id){if(current.OutletId is not Guid oid)return false;var x=(await codes.GetByOutletAsync(oid)).FirstOrDefault(y=>y.Id==id);if(x is null)return false;x.IsActive=false;await codes.UpdateAsync(x);return true;}
    private static DiscountCodeDto Map(DiscountCode x)=>new(x.Id,x.OutletId,x.Code,x.Percent,x.MaxAmount,x.MaxRedemptions,x.RedemptionCount,x.StartsAtUtc,x.EndsAtUtc,x.IsActive);
}
