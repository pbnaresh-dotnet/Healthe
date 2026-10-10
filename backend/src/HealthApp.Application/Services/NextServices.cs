using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using HealthApp.Application.Abstractions;
using HealthApp.Application.Orchestration;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class CustomerProfileService(ICurrentUser current, ICustomerProfileRepository profiles, IAllergenRepository allergens, ICustomerAllergyRepository customerAllergies, IUnitOfWork unitOfWork) : ICustomerProfileService
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
        await unitOfWork.ExecuteAsync(async () =>
        {
            await profiles.AddOrUpdateAsync(p);
            await customerAllergies.ReplaceAsync(id,requested);
        });
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
    public async Task<SubscriptionDiscountTierDto?> AddTierAsync(SaveSubscriptionDiscountTierRequest r){if(current.OutletId is not Guid id)return null;await ValidateAsync(id, r);var x=new SubscriptionDiscountTier{Id=Guid.NewGuid(),OutletId=id,MinMeals=r.MinMeals,MaxMeals=r.MaxMeals,OneWeekPercent=r.OneWeekPercent,TwoWeeksPercent=r.TwoWeeksPercent,OneMonthPercent=r.OneMonthPercent,IsActive=r.IsActive};await tiers.AddAsync(x);return Map(x);}
    public async Task<SubscriptionDiscountTierDto?> UpdateTierAsync(Guid id,SaveSubscriptionDiscountTierRequest r){if(current.OutletId is not Guid oid)return null;var x=(await tiers.GetByOutletAsync(oid)).FirstOrDefault(y=>y.Id==id);if(x is null)return null;await ValidateAsync(oid, r, id);x.MinMeals=r.MinMeals;x.MaxMeals=r.MaxMeals;x.OneWeekPercent=r.OneWeekPercent;x.TwoWeeksPercent=r.TwoWeeksPercent;x.OneMonthPercent=r.OneMonthPercent;x.IsActive=r.IsActive;await tiers.UpdateAsync(x);return Map(x);}
    public async Task<bool> DeleteTierAsync(Guid id){if(current.OutletId is not Guid oid)return false;await tiers.DeleteAsync(oid,id);return true;}
    private async Task ValidateAsync(Guid outletId, SaveSubscriptionDiscountTierRequest r, Guid? excludeId = null)
    {
        if (r.MinMeals < 1 || (r.MaxMeals.HasValue && r.MaxMeals.Value < r.MinMeals) ||
            new[] { r.OneWeekPercent, r.TwoWeeksPercent, r.OneMonthPercent }.Any(x => x < 0m || x > 100m))
            throw new ArgumentException("Invalid discount tier. Meal ranges must be valid and discount percentages must be between 0% and 100%.");

        var existing = await tiers.GetByOutletAsync(outletId);
        var candidateMax = r.MaxMeals ?? int.MaxValue;
        if (existing.Any(x => x.IsActive && x.Id != excludeId &&
            r.MinMeals <= (x.MaxMeals ?? int.MaxValue) && x.MinMeals <= candidateMax))
            throw new ArgumentException("Discount meal ranges overlap an existing active tier. Use non-overlapping ranges.");
    }
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

public sealed class PaymentService(
    ICurrentUser current,
    IUserRepository users,
    ICustomerAddressRepository addresses,
    IOutletOnboardingRepository onboardingApplications,
    IPaymentTransactionRepository payments,
    ISubscriptionRepository subscriptions,
    IOrderRepository orders,
    IOutletPackageActivationService outletPackageActivation,
    IOutletRepository outlets,
    IOutletDomainRepository outletDomains,
    IOptions<TenantDomainSettings> tenantDomainSettings,
    IPaymentGateway gateway,
    ITransactionalEmailService emails,
    IConfiguration configuration) : IPaymentService
{
    public async Task<PaymentCheckoutDto?> CreateAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (current.UserId is not Guid customerId)
            return null;

        if (current.OutletId is not Guid customerOutletId)
            throw new UnauthorizedAccessException("Customer outlet context is required.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ArgumentException("Idempotency key is required.");
        if (request.IdempotencyKey.Trim().Length > 200)
            throw new ArgumentException("Idempotency key must not exceed 200 characters.");

        if (!string.Equals(request.Provider, gateway.Provider, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported payment provider '{request.Provider}'. Use {gateway.Provider}.");

        var subscription = await subscriptions.GetAsync(request.SubscriptionId)
            ?? throw new KeyNotFoundException("Subscription not found.");

        if (subscription.CustomerId != customerId || subscription.OutletId != customerOutletId)
            throw new UnauthorizedAccessException("The subscription does not belong to the current customer.");

        if (subscription.PackageStatus == "PendingOutletReview")
            throw new InvalidOperationException("This package is awaiting outlet confirmation before payment.");

        if (subscription.IsOutletCreated && subscription.PackageStatus != "PaymentPending")
            throw new InvalidOperationException("Accept the outlet-created package before making payment.");

        var fingerprint = ComputePaymentFingerprint(
            gateway.Provider,
            subscription.Id,
            subscription.TotalCharged,
            "INR");

        var existing = await payments.GetByIdempotencyKeyAsync(gateway.Provider, request.IdempotencyKey.Trim());
        if (existing is not null)
        {
            if (existing.CustomerId != customerId ||
                existing.SubscriptionId != subscription.Id ||
                !string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The payment idempotency key is already in use for a different payment request.");

            if (!string.IsNullOrWhiteSpace(existing.PaymentSessionId) || existing.Status == "Paid")
                return MapCheckout(existing);

            // A previous attempt may have created the provider order but failed before
            // our database update. Reuse the same provider order and idempotency key.
        }

        var customer = await users.FindByIdAsync(customerId)
            ?? throw new UnauthorizedAccessException("Customer account not found.");
        var defaultAddress = (await addresses.GetByCustomerAsync(customerId))
            .OrderByDescending(x => x.IsDefault)
            .FirstOrDefault();
        var customerPhone = NormalizePhone(customer.MobileNumber);
        if (customerPhone.Length < 10)
            customerPhone = NormalizePhone(defaultAddress?.ContactPhone);
        if (customerPhone.Length < 10)
            throw new InvalidOperationException("A valid customer mobile number is required before payment.");

        var payment = existing ?? new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OutletId = subscription.OutletId,
            SubscriptionId = subscription.Id,
            PaymentType = "CustomerSubscription",
            Provider = gateway.Provider,
            ProviderOrderId = $"BRC-CUS-{Guid.NewGuid():N}",
            IdempotencyKey = request.IdempotencyKey.Trim(),
            RequestFingerprint = fingerprint,
            ProcessingStatus = "CreatingProviderOrder",
            AttemptCount = 0,
            Amount = subscription.TotalCharged,
            Currency = "INR",
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };

        if (existing is null)
        {
            try
            {
                await payments.AddAsync(payment);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                var concurrent = await payments.GetByIdempotencyKeyAsync(payment.Provider, payment.IdempotencyKey);
                if (concurrent is null)
                    throw;
                if (!string.Equals(concurrent.RequestFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The payment idempotency key is already in use for a different payment request.");
                payment = concurrent;
            }
        }

        try
        {
            payment.AttemptCount++;
            payment.LastAttemptAtUtc = DateTime.UtcNow;
            await payments.UpdateAsync(payment);

            var checkout = await gateway.CreateOrderAsync(
                new PaymentGatewayCreateOrderRequest(
                    payment.ProviderOrderId,
                    payment.Amount,
                    payment.Currency,
                    customer.Id.ToString("N"),
                    $"{customer.FirstName} {customer.LastName}".Trim(),
                    customer.Email,
                    customerPhone,
                    await GetCustomerReturnUrlAsync(subscription.OutletId),
                    gateway.WebhookUrl,
                    $"Broccoly meal subscription {subscription.PlanName}",
                    payment.IdempotencyKey),
                cancellationToken);

            payment.ProcessingStatus = "ProviderOrderCreated";
            payment.NextRetryAtUtc = null;
            payment.PaymentSessionId = checkout.PaymentSessionId;
            payment.ProviderStatus = checkout.Status;
            payment.GatewayResponseJson = JsonSerializer.Serialize(new
            {
                checkout.ProviderOrderId,
                checkout.PaymentSessionId,
                checkout.Status
            });
            await payments.UpdateAsync(payment);

            return MapCheckout(payment);
        }
        catch (Exception ex)
        {
            payment.ProcessingStatus = "ProviderOrderCreationFailed";
            payment.LastErrorCode = ex.GetType().Name;
            payment.NextRetryAtUtc = DateTime.UtcNow.AddSeconds(Math.Min(300, 10 * Math.Pow(2, Math.Max(0, payment.AttemptCount - 1))));
            payment.Status = "Pending";
            payment.FailureReason = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
            await payments.UpdateAsync(payment);
            throw;
        }
    }

    public async Task RetryPendingAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await payments.GetAsync(paymentId);
        if (payment is null ||
            payment.Status != "Pending" ||
            payment.PaymentType != "CustomerSubscription" ||
            payment.SubscriptionId is not Guid subscriptionId)
            return;

        if (!string.Equals(payment.Provider, gateway.Provider, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Payment provider '{payment.Provider}' is not the configured gateway '{gateway.Provider}'.");

        var subscription = await subscriptions.GetAsync(subscriptionId)
            ?? throw new KeyNotFoundException("Subscription not found.");

        var customerId = payment.CustomerId
            ?? throw new InvalidOperationException("Customer payment is missing its customer.");

        var customer = await users.FindByIdAsync(customerId)
            ?? throw new UnauthorizedAccessException("Customer account not found.");

        var defaultAddress = (await addresses.GetByCustomerAsync(customerId))
            .OrderByDescending(x => x.IsDefault)
            .FirstOrDefault();

        var customerPhone = NormalizePhone(customer.MobileNumber);
        if (customerPhone.Length < 10)
            customerPhone = NormalizePhone(defaultAddress?.ContactPhone);
        if (customerPhone.Length < 10)
            throw new InvalidOperationException("A valid customer mobile number is required before payment retry.");

        try
        {
            payment.AttemptCount++;
            payment.LastAttemptAtUtc = DateTime.UtcNow;
            payment.ProcessingStatus = "RetryingProviderOrder";
            await payments.UpdateAsync(payment);

            var checkout = await gateway.CreateOrderAsync(
                new PaymentGatewayCreateOrderRequest(
                    payment.ProviderOrderId,
                    payment.Amount,
                    payment.Currency,
                    customer.Id.ToString("N"),
                    $"{customer.FirstName} {customer.LastName}".Trim(),
                    customer.Email,
                    customerPhone,
                    await GetCustomerReturnUrlAsync(subscription.OutletId),
                    gateway.WebhookUrl,
                    $"Broccoly meal subscription {subscription.PlanName}",
                    payment.IdempotencyKey),
                cancellationToken);

            payment.ProcessingStatus = "ProviderOrderCreated";
            payment.NextRetryAtUtc = null;
            payment.LastErrorCode = "";
            payment.FailureReason = "";
            payment.PaymentSessionId = checkout.PaymentSessionId;
            payment.ProviderStatus = checkout.Status;
            payment.GatewayResponseJson = JsonSerializer.Serialize(new
            {
                checkout.ProviderOrderId,
                checkout.PaymentSessionId,
                checkout.Status
            });
            await payments.UpdateAsync(payment);
        }
        catch (Exception ex)
        {
            payment.ProcessingStatus = "ProviderOrderCreationFailed";
            payment.LastErrorCode = ex.GetType().Name;
            payment.NextRetryAtUtc = DateTime.UtcNow.AddSeconds(
                Math.Min(300, 10 * Math.Pow(2, Math.Max(0, payment.AttemptCount - 1))));
            payment.Status = "Pending";
            payment.FailureReason = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
            await payments.UpdateAsync(payment);
            throw;
        }
    }

    public async Task<PaymentDto?> GetAsync(Guid id)
    {
        if (current.UserId is not Guid uid)
            return null;

        var payment = await payments.GetAsync(id);
        if (payment is null || payment.CustomerId != uid)
            return null;

        if (payment.SubscriptionId is not Guid subscriptionId)
            return null;

        var subscription = await subscriptions.GetAsync(subscriptionId);
        if (subscription is null ||
            subscription.CustomerId != uid ||
            current.OutletId != subscription.OutletId)
            return null;

        if (!string.IsNullOrWhiteSpace(payment.ProviderOrderId))
        {
            var now = DateTime.UtcNow;
            if (await payments.TryClaimPaymentProcessingAsync(payment.Id, now, now.AddMinutes(-5)))
            {
                payment.ProcessingStatus = "WebhookProcessing";
                payment.LastAttemptAtUtc = now;
                try
                {
                    if (payment.Status != "Paid")
                        await RefreshFromGatewayAsync(payment, CancellationToken.None, preserveWebhookClaim: true);

                    // The same atomic lease protects status polling and webhooks from fulfilling
                    // the same payment concurrently. Paid retries also repair interrupted fulfilment.
                    if (payment.Status == "Paid")
                        await CompleteCustomerPaymentAsync(payment.Id);

                    payment.ProcessingStatus = payment.Status == "Paid" ? "PaymentVerified" : "WebhookProcessed";
                    await payments.UpdateAsync(payment);
                }
                catch
                {
                    payment.ProcessingStatus = "WebhookProcessingFailed";
                    await payments.UpdateAsync(payment);
                    throw;
                }
            }
        }

        return Map(payment);
    }

    public async Task<PaymentWebhookResultDto> HandleWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        if (!gateway.VerifyWebhookSignature(headers, rawBody))
            throw new UnauthorizedAccessException("Invalid payment gateway webhook signature.");

        var webhook = gateway.ParseWebhook(rawBody);
        if (webhook is null || string.IsNullOrWhiteSpace(webhook.ProviderOrderId))
            return new PaymentWebhookResultDto(true, "Ignored");

        var providerOrderId = webhook.ProviderOrderId;

        var payment = await payments.GetByProviderOrderIdAsync(providerOrderId);
        if (payment is null)
            return new PaymentWebhookResultDto(true, "UnknownOrder");

        var now = DateTime.UtcNow;
        if (!await payments.TryClaimPaymentProcessingAsync(payment.Id, now, now.AddMinutes(-5)))
            return new PaymentWebhookResultDto(true, "AlreadyProcessing", payment.Id);

        // ExecuteUpdateAsync claims the row atomically, so align this tracked instance with
        // the lease before RefreshFromGatewayAsync persists any verified provider fields.
        payment.ProcessingStatus = "WebhookProcessing";
        payment.LastAttemptAtUtc = now;
        try
        {
            // Verify the actual provider status while holding the short-lived processing lease.
            await RefreshFromGatewayAsync(payment, cancellationToken, rawBody, preserveWebhookClaim: true);

            if (payment.Status == "Paid")
            {
                // Run idempotent fulfilment even on a replay. This repairs a prior interruption
                // after payment verification but before package activation/onboarding completion.
                if (payment.PaymentType == "CustomerSubscription")
                    await CompleteCustomerPaymentAsync(payment.Id);
                else if (payment.PaymentType == "OutletOnboarding")
                    await CompleteOutletOnboardingPaymentAsync(payment);
            }

            payment.ProcessingStatus = payment.Status == "Paid" ? "PaymentVerified" : "WebhookProcessed";
            await payments.UpdateAsync(payment);
            return new PaymentWebhookResultDto(true, payment.Status, payment.Id);
        }
        catch
        {
            // Do not strand the order on a live lease after a provider/fulfilment error.
            payment.ProcessingStatus = "WebhookProcessingFailed";
            await payments.UpdateAsync(payment);
            throw;
        }
    }

    private async Task RefreshFromGatewayAsync(
        PaymentTransaction payment,
        CancellationToken cancellationToken,
        string? webhookBody = null,
        bool preserveWebhookClaim = false)
    {
        var transactions = await gateway.GetPaymentsAsync(payment.ProviderOrderId, cancellationToken);
        var latest = transactions
            .OrderByDescending(x => string.Equals(x.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (latest is null)
            return;

        payment.ProviderStatus = latest.PaymentStatus;
        payment.ProviderPaymentId = latest.ProviderPaymentId;
        payment.PaymentMethod = latest.PaymentMethod ?? "";
        if (webhookBody is not null)
            payment.GatewayResponseJson = webhookBody;

        var normalized = latest.PaymentStatus.Trim().ToUpperInvariant();
        if (normalized == "SUCCESS")
        {
            if ((latest.Amount.HasValue &&
                 Math.Abs(latest.Amount.Value - payment.Amount) > 0.01m) ||
                !string.Equals(latest.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
            {
                payment.Status = "Failed";
                payment.FailureReason = $"{gateway.Provider} payment amount or currency does not match the Broccoly order.";
            }
            else
            {
                payment.Status = "Paid";
                payment.ProcessingStatus = preserveWebhookClaim ? "WebhookProcessing" : "PaymentVerified";
                payment.FailureReason = "";
                payment.NextRetryAtUtc = null;
                payment.PaidAtUtc ??= DateTime.UtcNow;
            }
        }
        else if (payment.Status == "Paid")
        {
            // Never downgrade a previously verified payment because a later webhook/status
            // response is stale, delayed or represents a non-success attempt.
            payment.ProcessingStatus = preserveWebhookClaim ? "WebhookProcessing" : "PaymentVerified";
        }
        else if (normalized == "PENDING")
        {
            payment.Status = "Pending";
        }
        else
        {
            payment.Status = "Failed";
            payment.FailureReason = latest.PaymentMessage ?? "Cashfree payment was not successful.";
        }

        await payments.UpdateAsync(payment);
    }

    private static string ComputePaymentFingerprint(string provider, Guid subscriptionId, decimal amount, string currency)
    {
        var canonical = $"{provider.Trim().ToUpperInvariant()}|{subscriptionId:N}|{amount:F2}|{currency.Trim().ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }


    private async Task<string> GetCustomerReturnUrlAsync(Guid outletId)
    {
        var customDomain = (await outletDomains.GetByOutletAsync(outletId))
            .Where(x => x.Status == OutletDomainStatus.Active &&
                        x.IsPrimary &&
                        !string.IsNullOrWhiteSpace(x.Hostname))
            .OrderByDescending(x => x.VerifiedAtUtc ?? x.CreatedAtUtc)
            .FirstOrDefault();

        var hostname = customDomain?.Hostname?.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(hostname))
        {
            var outlet = await outlets.GetByIdAsync(outletId)
                ?? throw new KeyNotFoundException("Outlet not found.");
            var subdomain = outlet.Subdomain.Trim().Trim('.');
            var baseDomain = tenantDomainSettings.Value.PlatformBaseDomain.Trim().Trim('.');
            if (string.IsNullOrWhiteSpace(subdomain) || string.IsNullOrWhiteSpace(baseDomain))
                throw new InvalidOperationException("Customer storefront domain is not configured for this outlet.");

            hostname = $"{subdomain}.{baseDomain}";
        }

        if (!Uri.TryCreate($"https://{hostname}/payment", UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("Customer storefront domain is invalid.");

        return uri.AbsoluteUri.TrimEnd('/');
    }

    private async Task CompleteOutletOnboardingPaymentAsync(PaymentTransaction payment)
    {
        if (payment.OutletOnboardingApplicationId is not Guid applicationId)
            return;

        var application = await onboardingApplications.GetAsync(applicationId)
            ?? throw new KeyNotFoundException("Outlet onboarding application not found.");

        if (application.PaymentStatus == "Paid")
            return;

        if (payment.Amount <= 0 ||
            payment.Status != "Paid")
            return;

        application.PaymentStatus = "Paid";
        application.PaymentReference = string.IsNullOrWhiteSpace(payment.ProviderPaymentId)
            ? payment.ProviderOrderId
            : payment.ProviderPaymentId;
        if (application.Status == "PaymentPending")
            application.Status = "PendingVerification";

        if (application.UserId is Guid userId)
        {
            var user = await users.FindByIdAsync(userId);
            if (user is not null && !user.IsActive)
            {
                user.IsActive = true;
                await users.UpdateAsync(user);
            }
        }

        await onboardingApplications.UpdateAsync(application);

        var outlet = application.OutletId is Guid outletId
            ? await outlets.GetByIdAsync(outletId)
            : null;
        if (outlet is not null)
        {
            await emails.TrySendAsync(
                EmailTemplateId.OutletOnboardingPaymentConfirmed,
                application.Email,
                new Dictionary<string, string?>
                {
                    ["OwnerName"] = application.OwnerName,
                    ["OutletName"] = application.OutletName,
                    ["PlanName"] = application.PlanName,
                    ["BillingCycle"] = application.BillingCycle,
                    ["SetupFee"] = $"₹{application.SetupFee:N2}",
                    ["PaymentReference"] = application.PaymentReference,
                    ["OutletAdminUrl"] = configuration["Email:OutletAdminUrl"] ?? "https://outlet.broccoly.in"
                });
        }
    }

    private async Task CompleteCustomerPaymentAsync(Guid paymentId)
    {
        var payment = await payments.GetAsync(paymentId)
            ?? throw new KeyNotFoundException("Payment transaction not found.");

        if (payment.Status != "Paid" || payment.SubscriptionId is not Guid subscriptionId)
            return;

        // Payment completion is deliberately idempotent: activation itself is state-guarded
        // and will return safely when the package is already active.

        var subscription = await subscriptions.GetAsync(subscriptionId)
            ?? throw new KeyNotFoundException("Subscription not found.");

        if (subscription.PackageStatus == "PaymentPending")
        {
            await outletPackageActivation.ActivateAsync(subscription.Id, gateway.Provider, payment.CustomerId!.Value);
            return;
        }

        var order = await orders.GetBySubscriptionAsync(subscription.Id);
        if (order is not null)
        {
            order.Status = OrderStatus.Confirmed;
            await orders.UpdateAsync(order);
        }
    }

    private static string NormalizePhone(string? value)
    {
        var phone = new string((value ?? "").Where(char.IsDigit).ToArray());
        return phone.Length >= 10 ? phone[^10..] : phone;
    }

    private static string? ExtractString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(segment, out element))
                return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static PaymentCheckoutDto MapCheckout(PaymentTransaction p) =>
        new(p.Id, p.Provider, p.ProviderOrderId, p.PaymentSessionId, p.Amount, p.Currency, p.Status);

    private static PaymentDto Map(PaymentTransaction p) =>
        new(
            p.Id,
            p.SubscriptionId,
            p.Provider,
            p.ProviderPaymentId,
            p.ProviderOrderId,
            p.Amount,
            p.Currency,
            p.Status,
            p.PaymentMethod,
            p.FailureReason,
            p.CreatedAtUtc,
            p.PaidAtUtc);
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
    IUserRepository users,
    IDeliveryRouteRepository routes) : IDeliveryLabelService
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

        var routeStatusMap = new Dictionary<Guid, (int Sequence, string Status)>();
        foreach (var group in rows.Where(x => x.RouteId.HasValue).GroupBy(x => new { x.ScheduledDate.Date, x.MealSlot }))
        {
            var routeRows = await routes.GetByOutletAndDateAsync(id, group.Key.Date, group.Key.MealSlot);
            foreach (var route in routeRows)
                foreach (var stop in route.Stops)
                    foreach (var delivery in group.Where(x => x.RouteStopId == stop.Id))
                        routeStatusMap[delivery.Id] = (stop.StopSequence, route.Status.ToString());
        }

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
                var recipe=await recipes.GetForOutletAsync(meal.RecipeId, id);
                var slot=GetMealSlotInfo(delivery.MealSlot);

                result.Add(new DeliveryLabelDto(
                    subscription.Id,
                    meal.Id,
                    meal.MealDate,
                    (int)delivery.MealSlot,
                    slot.Name,
                    slot.Window,
                    outlet.Name,
                    string.IsNullOrWhiteSpace(outlet.Branding?.LogoUrl) ? outlet.LogoUrl : outlet.Branding.LogoUrl,
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
                    delivery.DeliveryFee,
                    delivery.Id,
                    delivery.Status.ToString(),
                    delivery.RouteId,
                    routeStatusMap.TryGetValue(delivery.Id, out var routeInfo) ? routeInfo.Sequence : delivery.RouteSequence,
                    routeStatusMap.TryGetValue(delivery.Id, out routeInfo) ? routeInfo.Status : "",
                    meal.Status.ToString()));
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
