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

public sealed class CustomerAddressService(
    ICurrentUser current,
    ICustomerAddressRepository addresses,
    IServiceCityRepository serviceCities,
    ICityAreaRepository areas,
    IGeocodingService geocoding,
    IDeliveryCalculator calculator) : ICustomerAddressService
{
    public async Task<IReadOnlyList<CustomerAddressDto>> GetAsync() =>
        current.UserId is not Guid id ? [] : await Map(await addresses.GetByCustomerAsync(id));

    public async Task<CustomerAddressDto?> CreateAsync(CreateCustomerAddressRequest r)
    {
        if (current.UserId is not Guid id)
            return null;

        var address = await BuildAddressAsync(id, r.City, r.Pincode, r.Locality, r.CityAreaId, r.Label, r.AddressLine1, r.AddressLine2, r.ContactName, r.ContactPhone, r.Latitude, r.Longitude, r.IsDefault);
        await addresses.AddAsync(address);
        return (await Map(new[] { address })).First();
    }

    public async Task<CustomerAddressDto?> UpdateAsync(Guid addressId, UpdateCustomerAddressRequest r)
    {
        if (current.UserId is not Guid id)
            return null;

        var address = await addresses.GetAsync(id, addressId);
        if (address is null)
            return null;

        var replacement = await BuildAddressAsync(id, r.City, r.Pincode, r.Locality, r.CityAreaId, r.Label, r.AddressLine1, r.AddressLine2, r.ContactName, r.ContactPhone, r.Latitude, r.Longitude, r.IsDefault);
        address.City = replacement.City;
        address.State = replacement.State;
        address.Pincode = replacement.Pincode;
        address.Locality = replacement.Locality;
        address.CityAreaId = replacement.CityAreaId;
        address.Label = replacement.Label;
        address.AddressLine1 = replacement.AddressLine1;
        address.AddressLine2 = replacement.AddressLine2;
        address.ContactName = replacement.ContactName;
        address.ContactPhone = replacement.ContactPhone;
        address.Latitude = replacement.Latitude;
        address.Longitude = replacement.Longitude;
        address.IsDefault = replacement.IsDefault;

        await addresses.UpdateAsync(address);
        return (await Map(new[] { address })).First();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (current.UserId is not Guid uid)
            return false;
        var address = await addresses.GetAsync(uid, id);
        if (address is null)
            return false;
        await addresses.DeleteAsync(uid, id);
        return true;
    }

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsInfinity(latitude) || latitude is < -90 or > 90)
            throw new ArgumentException("Latitude must be between -90 and 90.");
        if (double.IsNaN(longitude) || double.IsInfinity(longitude) || longitude is < -180 or > 180)
            throw new ArgumentException("Longitude must be between -180 and 180.");
    }

    private async Task<CustomerAddress> BuildAddressAsync(
        Guid customerId,
        string city,
        string? pincode,
        string? locality,
        Guid? cityAreaId,
        string label,
        string addressLine1,
        string addressLine2,
        string contactName,
        string contactPhone,
        double latitude,
        double longitude,
        bool isDefault)
    {
        ValidateCoordinates(latitude, longitude);

        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("Delivery city is required.");

        var serviceCity = await serviceCities.GetByCityAsync(city.Trim());
        if (serviceCity is null || !serviceCity.IsEnabled)
            throw new ArgumentException($"{city.Trim()} is not currently supported by HealthApp.");

        if (string.IsNullOrWhiteSpace(addressLine1))
            throw new ArgumentException("Address line 1 is required.");

        ReverseGeocodeDto? resolved = null;
        try
        {
            resolved = await geocoding.ReverseAsync(latitude, longitude);
        }
        catch
        {
            // Geocoding enriches the address but is not required to save a valid city + coordinate location.
        }

        if (resolved is not null &&
            !string.IsNullOrWhiteSpace(resolved.City) &&
            !resolved.City.Equals(serviceCity.City, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The map pin resolves to {resolved.City}, but the selected delivery city is {serviceCity.City}. Please place the pin inside {serviceCity.City}.");
        }

        var resolvedPincode = string.IsNullOrWhiteSpace(pincode) ? resolved?.Pincode : pincode.Trim();
        var resolvedLocality = string.IsNullOrWhiteSpace(locality)
            ? (resolved?.Suburb ?? resolved?.Neighbourhood ?? string.Empty)
            : locality.Trim();

        Guid? resolvedAreaId = null;
        if (cityAreaId.HasValue)
        {
            var area = await areas.GetAsync(cityAreaId.Value);
            if (area is not null &&
                area.IsActive &&
                area.City.Equals(serviceCity.City, StringComparison.OrdinalIgnoreCase))
            {
                resolvedAreaId = area.Id;
                if (string.IsNullOrWhiteSpace(resolvedLocality))
                    resolvedLocality = area.Name;
                if (string.IsNullOrWhiteSpace(resolvedPincode))
                    resolvedPincode = area.Pincode;
            }
        }

        return new CustomerAddress
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CityAreaId = resolvedAreaId,
            City = serviceCity.City,
            State = serviceCity.State,
            Pincode = resolvedPincode?.Trim() ?? string.Empty,
            Locality = resolvedLocality,
            Label = label?.Trim() ?? string.Empty,
            AddressLine1 = addressLine1.Trim(),
            AddressLine2 = addressLine2?.Trim() ?? string.Empty,
            ContactName = contactName?.Trim() ?? string.Empty,
            ContactPhone = contactPhone?.Trim() ?? string.Empty,
            Latitude = latitude,
            Longitude = longitude,
            IsDefault = isDefault
        };
    }

    public async Task<IReadOnlyList<DeliveryQuoteDto>> QuoteAsync(Guid outletId)
    {
        if (current.UserId is not Guid id)
            return [];

        if (current.OutletId is not Guid customerOutletId || customerOutletId != outletId)
            throw new UnauthorizedAccessException("The current customer is not associated with the selected outlet.");

        var result = new List<DeliveryQuoteDto>();
        foreach (var address in await addresses.GetByCustomerAsync(id))
        {
            try
            {
                result.Add(await calculator.QuoteAsync(outletId, id, address.Id));
            }
            catch
            {
                // An address may be valid but not serviceable by this outlet.
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<CustomerAddressDto>> Map(IEnumerable<CustomerAddress> rows)
    {
        return rows.Select(a => new CustomerAddressDto(
            a.Id,
            a.Label,
            string.IsNullOrWhiteSpace(a.Locality) ? string.Empty : a.Locality,
            a.City,
            a.Pincode,
            a.AddressLine1,
            a.AddressLine2,
            a.ContactName,
            a.ContactPhone,
            a.Latitude,
            a.Longitude,
            a.IsDefault)).ToList();
    }
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

public sealed class ServiceCityAdminService(IServiceCityRepository cities) : IServiceCityAdminService
{
    public async Task<IReadOnlyList<ServiceCityDto>> GetAsync() =>
        (await cities.GetAllAsync())
            .Select(x => new ServiceCityDto(x.Id, x.City, x.State, x.Country, x.Latitude, x.Longitude, x.IsEnabled))
            .ToList();

    public async Task<ServiceCityDto?> CreateAsync(CreateServiceCityRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.City))
            throw new ArgumentException("City is required.");

        var existing = await cities.GetByCityAsync(r.City.Trim());
        if (existing is not null)
        {
            existing.State = r.State.Trim();
            existing.Country = string.IsNullOrWhiteSpace(r.Country) ? "India" : r.Country.Trim();
            existing.Latitude = r.Latitude;
            existing.Longitude = r.Longitude;
            existing.IsEnabled = r.IsEnabled;
            await cities.UpdateAsync(existing);
            return new(existing.Id, existing.City, existing.State, existing.Country, existing.Latitude, existing.Longitude, existing.IsEnabled);
        }

        var x = new ServiceCity
        {
            Id = Guid.NewGuid(),
            City = r.City.Trim(),
            State = r.State.Trim(),
            Country = string.IsNullOrWhiteSpace(r.Country) ? "India" : r.Country.Trim(),
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            IsEnabled = r.IsEnabled
        };

        await cities.AddAsync(x);
        return new(x.Id, x.City, x.State, x.Country, x.Latitude, x.Longitude, x.IsEnabled);
    }

    public async Task<ServiceCityDto?> SetEnabledAsync(Guid id, bool enabled)
    {
        var city = await cities.GetByIdAsync(id);
        if (city is null)
            return null;

        city.IsEnabled = enabled;
        await cities.UpdateAsync(city);
        return new(city.Id, city.City, city.State, city.Country, city.Latitude, city.Longitude, city.IsEnabled);
    }
}

public sealed class CityAreaAdminService(ICityAreaRepository areas) : ICityAreaAdminService
{
    public async Task<IReadOnlyList<CityAreaDto>> GetAsync(string? city)=>(await areas.GetActiveAsync(city)).Select(x=>new CityAreaDto(x.Id,x.City,x.State,x.Name,x.Pincode,x.Latitude,x.Longitude,x.IsActive)).ToList();
    public async Task<CityAreaDto?> CreateAsync(CreateCityAreaRequest r){var x=new CityArea{Id=Guid.NewGuid(),City=r.City,State=r.State,Name=r.Name,Pincode=r.Pincode,Latitude=r.Latitude,Longitude=r.Longitude};await areas.AddAsync(x);return new(x.Id,x.City,x.State,x.Name,x.Pincode,x.Latitude,x.Longitude,x.IsActive);}
}

public sealed class PaymentService(ICurrentUser current,IPaymentTransactionRepository payments,ISubscriptionRepository subscriptions,IOrderRepository orders,IOutletPackageActivationService outletPackageActivation) : IPaymentService
{
    public async Task<PaymentDto?> CreateAsync(CreatePaymentRequest r)
    {
        if(current.UserId is not Guid id)return null;
        if(string.IsNullOrWhiteSpace(r.IdempotencyKey))throw new ArgumentException("Idempotency key is required.");
        var s=await subscriptions.GetAsync(r.SubscriptionId)??throw new KeyNotFoundException("Subscription not found.");
        if(s.CustomerId!=id)throw new UnauthorizedAccessException();
        if(current.OutletId is not Guid customerOutletId || customerOutletId != s.OutletId)
            throw new UnauthorizedAccessException("The current customer is not associated with the subscription outlet.");
        if(s.IsOutletCreated&&s.PackageStatus!="PaymentPending")
            throw new InvalidOperationException("Accept the outlet-created package before making payment.");
        var existing=await payments.GetByIdempotencyKeyAsync(r.IdempotencyKey);
        if(existing is not null)
        {
            if(existing.CustomerId != id)
                throw new InvalidOperationException("The payment idempotency key is already in use.");
            return Map(existing);
        }
        var now=DateTime.UtcNow;
        var p=new PaymentTransaction{Id=Guid.NewGuid(),CustomerId=id,SubscriptionId=s.Id,Provider=r.Provider,ProviderPaymentId=$"mock_{Guid.NewGuid():N}",IdempotencyKey=r.IdempotencyKey,Amount=s.TotalCharged,Currency="INR",Status="Paid",CreatedAtUtc=now,PaidAtUtc=now};
        await payments.AddAsync(p);
        if(s.IsOutletCreated)
        {
            await outletPackageActivation.ActivateAsync(s.Id,"Online",id);
        }
        else
        {
            var order=await orders.GetBySubscriptionAsync(s.Id);
            if(order is not null){order.Status=OrderStatus.Confirmed;await orders.UpdateAsync(order);}
        }
        return Map(p);
    }
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
            var area=address?.CityAreaId is Guid areaId ? await areas.GetAsync(areaId) : null;
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
