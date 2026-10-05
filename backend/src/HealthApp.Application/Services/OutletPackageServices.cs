using HealthApp.Application.Abstractions;
using HealthApp.Application.Orchestration;
using HealthApp.Application.Strategies;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class OutletPackageActivationService(
    ISubscriptionRepository subscriptions,
    ISubscriptionMealSelectionRepository selections,
    IOrderRepository orders,
    IDeliveryRepository deliveries,
    ICustomerAddressRepository addresses,
    IUserRepository users,
    IPlatformTransactionRepository transactions,
    IUnitOfWork unitOfWork) : IOutletPackageActivationService
{
    public async Task<Subscription> ActivateAsync(Guid subscriptionId, string paymentMethod, Guid? paidByUserId)
    {
        var subscription = await subscriptions.GetAsync(subscriptionId)
            ?? throw new KeyNotFoundException("Subscription not found.");

        if (!subscription.IsOutletCreated)
            throw new InvalidOperationException("This subscription is not an outlet-created package.");

        var customer = await users.FindByIdAsync(subscription.CustomerId);
        if (customer is null || customer.Role != UserRole.Customer || customer.OutletId != subscription.OutletId)
            throw new InvalidOperationException("The package customer is not associated with the package outlet.");

        if (subscription.PackageStatus == "Active" && subscription.Status == SubscriptionStatus.Active)
            return subscription;

        if (subscription.PackageStatus is not ("PaymentPending" or "Paid" or "SentToCustomer"))
            throw new InvalidOperationException("This package cannot be activated in its current state.");

        var order = await orders.GetBySubscriptionAsync(subscription.Id)
            ?? throw new InvalidOperationException("Package order could not be found.");

        var mealRows = (await selections.GetBySubscriptionAsync(subscription.Id))
            .Where(x => x.Status != MealSelectionStatus.Cancelled)
            .OrderBy(x => x.MealDate)
            .ThenBy(x => x.MealSlot)
            .ToList();

        if (mealRows.Count == 0)
            throw new InvalidOperationException("The package contains no scheduled meals.");

        await unitOfWork.ExecuteAsync(async () =>
        {
            subscription.Status = SubscriptionStatus.Active;
            subscription.PackageStatus = "Active";
            subscription.PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Online" : paymentMethod.Trim();
            subscription.PaidByUserId = paidByUserId;
            subscription.PaidAtUtc ??= DateTime.UtcNow;
            subscription.AcceptedAtUtc ??= DateTime.UtcNow;
            order.Status = OrderStatus.Confirmed;

            await subscriptions.UpdateAsync(subscription);
            await orders.UpdateAsync(order);

            var existingDeliveries = await deliveries.GetBySubscriptionAsync(subscription.Id);
            if (existingDeliveries.Count == 0)
            {
                var groups = subscription.DeliveryMode == SubscriptionDeliveryMode.OneDeliveryPerDay
                    ? mealRows.GroupBy(x => x.MealDate.Date).Select(g => g.ToList())
                    : mealRows.Select(x => new List<SubscriptionMealSelection> { x });

                foreach (var group in groups)
                {
                    var first = group[0];
                    if (!first.AddressId.HasValue)
                        throw new InvalidOperationException("A delivery address is missing from the package.");

                    var address = await addresses.GetAsync(subscription.CustomerId, first.AddressId.Value)
                        ?? throw new InvalidOperationException("A delivery address could not be resolved.");

                    await deliveries.AddAsync(new Delivery
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        SubscriptionId = subscription.Id,
                        OutletId = subscription.OutletId,
                        CustomerId = subscription.CustomerId,
                        DeliveryAddressId = address.Id,
                        ScheduledDate = first.MealDate.Date,
                        MealSlot = subscription.DeliveryMode == SubscriptionDeliveryMode.OneDeliveryPerDay ? MealSlot.Afternoon : first.MealSlot,
                        CustomerName = customer is null ? "" : $"{customer.FirstName} {customer.LastName}".Trim(),
                        Address = $"{address.AddressLine1}, {address.AddressLine2}, {address.ContactPhone}".Trim(' ', ','),
                        DeliveryFee = first.DeliveryFee,
                        Status = DeliveryStatus.Scheduled
                    });
                }
            }

            var existingTx = await transactions.GetAllAsync();
            var alreadyRecorded = existingTx.Any(x => x.SubscriptionId == subscription.Id && x.Type == "CustomerSubscription");
            if (!alreadyRecorded)
            {
                await transactions.AddAsync(new PlatformTransaction
                {
                    Id = Guid.NewGuid(),
                    CustomerId = subscription.CustomerId,
                    OutletId = subscription.OutletId,
                    SubscriptionId = subscription.Id,
                    ReferenceId = $"outlet-package-{subscription.Id}",
                    Type = "CustomerSubscription",
                    GrossAmount = subscription.TotalCharged,
                    PlatformFee = subscription.PlatformServiceFee,
                    OutletAmount = subscription.OutletAmount,
                    FeePercent = subscription.PlatformServiceFeePercent,
                    Currency = "INR",
                    Status = "Paid"
                });
                await transactions.AddAsync(new PlatformTransaction
                {
                    Id = Guid.NewGuid(),
                    CustomerId = subscription.CustomerId,
                    OutletId = subscription.OutletId,
                    SubscriptionId = subscription.Id,
                    ReferenceId = $"outlet-commission-{subscription.Id}",
                    Type = "OutletCommission",
                    GrossAmount = subscription.OutletCommissionAmount,
                    PlatformFee = subscription.OutletCommissionAmount,
                    OutletAmount = 0,
                    FeePercent = subscription.OutletCommissionPercent * 100m,
                    Currency = "INR",
                    Status = "Paid"
                });
            }
        });

        return subscription;
    }
}

public sealed class OutletPackageService(
    ICurrentUser current,
    IUserRepository users,
    IPasswordService passwords,
    IOutletRepository outlets,
    IRecipeRepository recipes,
    IOutletMenuRepository menu,
    ISubscriptionRepository subscriptions,
    ISubscriptionMealSelectionRepository selections,
    ICustomerAddressRepository addresses,
    ICityAreaRepository areas,
    ICustomerProfileRepository profiles,
    IAllergenRepository allergens,
    ICustomerAllergyRepository customerAllergies,
    IMealPlanRepository mealPlans,
    IOrderRepository orders,
    IOrderFinancialRepository orderFinancials,
    IPaymentTransactionRepository payments,
    IDeliveryCalculator deliveryCalculator,
    IPlatformServiceFeeStrategy platformFee,
    ITaxStrategy taxStrategy,
    IMealPriceStrategy mealPrice,
    IAllergySafetyService allergySafety,
    IOutletSubscriptionRepository outletSubscriptions,
    IOutletPackageActivationService activation,
    IUnitOfWork unitOfWork) : IOutletPackageService
{
    public async Task<IReadOnlyList<UserDto>> GetCustomersAsync()
    {
        if (current.OutletId is not Guid outletId)
            return [];

        return (await users.GetAllAsync())
            .Where(x => x.Role == UserRole.Customer && x.OutletId == outletId)
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Select(MapUser)
            .ToList();
    }

    public async Task<UserDto?> CreateCustomerAsync(CreateOutletCustomerRequest request)
    {
        if (current.OutletId is not Guid outletId)
            return null;

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new ArgumentException("Customer first and last name are required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Customer email is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ArgumentException("Customer password must be at least 6 characters.");
        if (await users.FindByEmailAsync(request.Email, outletId) is not null)
            throw new InvalidOperationException("A customer with this email already exists for this outlet.");

        var requestedAllergies = (request.AllergyIds ?? []).Distinct().ToList();
        var validAllergies = await allergens.GetByIdsAsync(requestedAllergies);
        if (validAllergies.Count != requestedAllergies.Count)
            throw new ArgumentException("One or more selected allergies are invalid.");

        if (request.WeightKg is <= 0 || request.HeightCm is <= 0)
            throw new ArgumentException("Weight and height must be positive when supplied.");

        decimal? bmi = request.WeightKg.HasValue && request.HeightCm.HasValue
            ? Math.Round(request.WeightKg.Value / ((request.HeightCm.Value / 100m) * (request.HeightCm.Value / 100m)), 2)
            : null;

        var customer = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.Customer,
            OutletId = outletId,
            PasswordHash = passwords.Hash(request.Password),
            IsActive = true
        };

        await unitOfWork.ExecuteAsync(async () =>
        {
            await users.AddAsync(customer);

            await profiles.AddOrUpdateAsync(new CustomerProfile
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                WeightKg = request.WeightKg,
                HeightCm = request.HeightCm,
                Bmi = bmi,
                DateOfBirth = request.DateOfBirth,
                Goal = string.IsNullOrWhiteSpace(request.Goal) ? "WeightLoss" : request.Goal.Trim(),
                ActivityLevel = string.IsNullOrWhiteSpace(request.ActivityLevel) ? "Moderate" : request.ActivityLevel.Trim(),
                Diet = request.Diet?.Trim() ?? string.Empty,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await customerAllergies.ReplaceAsync(customer.Id, requestedAllergies);
        });

        return MapUser(customer);
    }

    public async Task<OutletCustomerProfileDto?> GetCustomerProfileAsync(Guid customerId)
    {
        await EnsureCustomerAccessAsync(customerId);
        var customer = await users.FindByIdAsync(customerId);
        if (customer is null || customer.Role != UserRole.Customer)
            return null;

        var profile = await profiles.GetAsync(customerId);
        var allergyRows = await customerAllergies.GetByCustomerAsync(customerId);
        var profileDto = profile is null
            ? null
            : new CustomerProfileDto(
                profile.Id,
                profile.CustomerId,
                profile.WeightKg,
                profile.HeightCm,
                profile.Bmi,
                profile.Goal,
                profile.ActivityLevel,
                profile.Diet,
                profile.UpdatedAtUtc,
                allergyRows.Select(a => new AllergenDto(a.AllergenId, a.Allergen.Name)).OrderBy(a => a.Name).ToList());

        return new OutletCustomerProfileDto(
            MapUser(customer),
            profileDto,
            await GetCustomerAddressesAsync(customerId));
    }

    public async Task<OutletCustomerProfileDto?> UpdateCustomerProfileAsync(Guid customerId, SaveCustomerProfileRequest request)
    {
        await EnsureCustomerAccessAsync(customerId);
        var customer = await users.FindByIdAsync(customerId);
        if (customer is null || customer.Role != UserRole.Customer)
            return null;

        if (request.WeightKg is <= 0 || request.HeightCm is <= 0)
            throw new ArgumentException("Weight and height must be positive when supplied.");

        var requestedAllergies = (request.AllergyIds ?? []).Distinct().ToList();
        var validAllergies = await allergens.GetByIdsAsync(requestedAllergies);
        if (validAllergies.Count != requestedAllergies.Count)
            throw new ArgumentException("One or more selected allergies are invalid.");

        var profile = await profiles.GetAsync(customerId) ?? new CustomerProfile
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId
        };
        profile.WeightKg = request.WeightKg;
        profile.HeightCm = request.HeightCm;
        profile.Bmi = request.WeightKg.HasValue && request.HeightCm.HasValue
            ? Math.Round(request.WeightKg.Value / ((request.HeightCm.Value / 100m) * (request.HeightCm.Value / 100m)), 2)
            : null;
        profile.DateOfBirth = request.DateOfBirth;
        profile.Goal = string.IsNullOrWhiteSpace(request.Goal) ? "WeightLoss" : request.Goal.Trim();
        profile.ActivityLevel = string.IsNullOrWhiteSpace(request.ActivityLevel) ? "Moderate" : request.ActivityLevel.Trim();
        profile.Diet = request.Diet?.Trim() ?? string.Empty;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        await unitOfWork.ExecuteAsync(async () =>
        {
            await profiles.AddOrUpdateAsync(profile);
            await customerAllergies.ReplaceAsync(customerId, requestedAllergies);
        });

        return await GetCustomerProfileAsync(customerId);
    }

    public async Task<IReadOnlyList<CustomerAddressDto>> GetCustomerAddressesAsync(Guid customerId)
    {
        await EnsureCustomerAccessAsync(customerId);
        var result = new List<CustomerAddressDto>();
        foreach (var address in await addresses.GetByCustomerAsync(customerId))
        {
            var areaName = address.CityAreaId is Guid areaId
                ? (await areas.GetAsync(areaId))?.Name ?? address.Locality
                : address.Locality;

            result.Add(new CustomerAddressDto(
                address.Id,
                address.Label,
                areaName ?? "",
                address.City,
                address.Pincode,
                address.AddressLine1,
                address.AddressLine2,
                address.ContactName,
                address.ContactPhone,
                address.Latitude,
                address.Longitude,
                address.IsDefault));
        }
        return result;
    }

    public async Task<CustomerAddressDto?> CreateCustomerAddressAsync(Guid customerId, OutletPackageAddressRequest request)
    {
        await EnsureCustomerAccessAsync(customerId);

        if (string.IsNullOrWhiteSpace(request.City))
            throw new ArgumentException("Address city is required.");
        if (string.IsNullOrWhiteSpace(request.AddressLine1))
            throw new ArgumentException("Address line 1 is required.");
        if (double.IsNaN(request.Latitude) || double.IsInfinity(request.Latitude) || request.Latitude is < -90 or > 90 ||
            double.IsNaN(request.Longitude) || double.IsInfinity(request.Longitude) || request.Longitude is < -180 or > 180)
            throw new ArgumentException("A valid map location is required.");

        var address = new CustomerAddress
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CityAreaId = request.CityAreaId,
            City = request.City.Trim(),
            Pincode = request.Pincode?.Trim() ?? "",
            Locality = request.Locality?.Trim() ?? "",
            Label = string.IsNullOrWhiteSpace(request.Label) ? "Home" : request.Label.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = request.AddressLine2?.Trim() ?? "",
            ContactName = request.ContactName?.Trim() ?? "",
            ContactPhone = request.ContactPhone?.Trim() ?? "",
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsDefault = request.IsDefault
        };

        await addresses.AddAsync(address);
        var areaName = address.CityAreaId is Guid areaId ? (await areas.GetAsync(areaId))?.Name ?? address.Locality : address.Locality;

        return new CustomerAddressDto(
            address.Id,
            address.Label,
            areaName ?? "",
            address.City,
            address.Pincode,
            address.AddressLine1,
            address.AddressLine2,
            address.ContactName,
            address.ContactPhone,
            address.Latitude,
            address.Longitude,
            address.IsDefault);
    }

    public Task<OutletPackageQuoteDto?> QuoteAsync(OutletPackageQuoteRequest request)
        => BuildQuoteAsync(request);

    public async Task<SubscriptionDto?> CreateAsync(CreateOutletPackageRequest request)
    {
        if (current.OutletId is not Guid outletId || current.UserId is not Guid outletUserId)
            return null;

        var quote = await BuildQuoteAsync(new OutletPackageQuoteRequest(
            request.CustomerId,
            outletId,
            request.DeliveryMode,
            request.Duration,
            request.Selections,
            request.DiscountType,
            request.DiscountValue,
            request.DiscountReason,
            request.ConfirmedAllergyRecipeIds,
            request.DeliveryCity));

        if (quote is null)
            return null;

        if (quote.RequiresAllergyConfirmation)
            throw new InvalidOperationException("Review and confirm the allergy warnings before sending this package.");

        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        var customer = await users.FindByIdAsync(request.CustomerId) ?? throw new KeyNotFoundException("Customer not found.");
        var duration = Parse<SubscriptionDuration>(request.Duration, "package duration");
        var deliveryMode = Parse<SubscriptionDeliveryMode>(request.DeliveryMode, "delivery mode");
        var mealRows = await BuildSelectionsAsync(request.Selections, outletId, outlet, request.CustomerId, deliveryMode);

        var start = mealRows.Min(x => x.MealDate).Date;
        var end = duration switch
        {
            SubscriptionDuration.ThreeDays => start.AddDays(2),
            SubscriptionDuration.FiveDays => start.AddDays(4),
            SubscriptionDuration.OneWeek => start.AddDays(6),
            SubscriptionDuration.TwoWeeks => start.AddDays(13),
            SubscriptionDuration.OneMonth => start.AddDays(27),
            _ => start
        };

        var plan = (await mealPlans.GetByOutletAsync(outletId)).FirstOrDefault(x => x.IsActive)
            ?? new MealPlan
            {
                Id = Guid.NewGuid(),
                OutletId = outletId,
                Name = "Custom Meal Package",
                Frequency = duration.ToString(),
                MealsPerDay = 0,
                MealsPerWeek = mealRows.Count,
                Price = 0,
                Currency = "INR",
                Description = "Package created by outlet"
            };

        if (plan.Price == 0 && plan.Name == "Custom Meal Package")
            await mealPlans.AddAsync(plan);

        var now = DateTime.UtcNow;
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            OutletId = outletId,
            DeliveryCity = request.DeliveryCity?.Trim() ?? outlet.City,
            MealPlanId = plan.Id,
            PlanName = $"{duration} Outlet Package",
            DeliveryMode = deliveryMode,
            Duration = duration,
            StartDate = start,
            EndDate = end,
            GrossMealAmount = quote.GrossMealAmount,
            SubscriptionDiscountPercent = quote.DiscountPercent,
            SubscriptionDiscountAmount = quote.DiscountAmount,
            NetMealAmount = quote.RestaurantTaxableAmount,
            PlatformServiceFee = quote.PlatformServiceFee,
            PlatformServiceGst = quote.PlatformServiceGst,
            PlatformServiceFeePercent = platformFee.Percent,
            PlatformServiceGstRate = quote.PlatformServiceFee == 0 ? 0 : Math.Round(quote.PlatformServiceGst / quote.PlatformServiceFee * 100m, 4),
            RestaurantGstRate = quote.RestaurantGstRate,
            RestaurantGstMode = Enum.Parse<GstMode>(quote.RestaurantGstMode, true),
            RestaurantTaxableAmount = quote.RestaurantTaxableAmount,
            RestaurantGstAmount = quote.RestaurantGstAmount,
            LateSkipFee = 0,
            Price = quote.RestaurantTaxableAmount,
            DeliveryFee = quote.DeliveryFee,
            CustomerTransactionFeePercent = 0,
            TransactionFee = 0,
            TotalCharged = quote.TotalCharged,
            OutletAmount = quote.OutletSettlementAmount,
            OutletCommissionPercent = quote.RestaurantTaxableAmount == 0 ? 0 : Math.Round(quote.OutletCommissionAmount / quote.RestaurantTaxableAmount, 6),
            OutletCommissionAmount = Math.Round(quote.RestaurantTaxableAmount - quote.OutletSettlementAmount, 2),
            DiscountCode = null,
            DiscountCodeAmount = 0,
            TotalMealCount = mealRows.Count,
            Frequency = "Weekly",
            MealsPerDay = 0,
            MealsPerWeek = mealRows.Count,
            Status = SubscriptionStatus.Pending,
            PackageStatus = "SentToCustomer",
            IsOutletCreated = true,
            CreatedByOutletUserId = outletUserId,
            OutletDiscountType = Parse<OutletPackageDiscountType>(request.DiscountType, "discount type"),
            OutletDiscountValue = Math.Max(0m, request.DiscountValue),
            OutletDiscountReason = request.DiscountReason?.Trim() ?? "",
            PaymentMethod = "Pending",
            SentAtUtc = now,
            NextDeliveryDate = start
        };

        foreach (var row in mealRows)
            row.SubscriptionId = subscription.Id;

        await unitOfWork.ExecuteAsync(async () =>
        {
            await subscriptions.AddAsync(subscription);
            await selections.AddRangeAsync(mealRows);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                OutletId = outletId,
                SubscriptionId = subscription.Id,
                Total = subscription.TotalCharged,
                Status = OrderStatus.Pending,
                DeliveryDate = start,
                Address = "Multiple scheduled delivery addresses",
                DeliveryAddressId = mealRows.Select(x => x.AddressId).FirstOrDefault(x => x.HasValue)
            };
            await orders.AddAsync(order);

            await orderFinancials.AddAsync(new OrderFinancialBreakdown
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                GrossMealAmount = quote.GrossMealAmount,
                DiscountAmount = quote.DiscountAmount,
                NetMealAmount = quote.RestaurantTaxableAmount,
                DeliveryAmount = quote.DeliveryFee,
                PlatformServiceFee = quote.PlatformServiceFee,
                PlatformServiceGst = quote.PlatformServiceGst,
                RestaurantGstRate = quote.RestaurantGstRate,
                RestaurantGstMode = Enum.Parse<GstMode>(quote.RestaurantGstMode, true),
                RestaurantTaxableAmount = quote.RestaurantTaxableAmount,
                RestaurantGstAmount = quote.RestaurantGstAmount,
                LateSkipFee = 0,
                CustomerPayable = quote.TotalCharged,
                OutletCommission = subscription.OutletCommissionAmount,
                OutletCommissionGst = 0,
                OutletSettlementAmount = subscription.OutletAmount,
                HealthAppRevenue = quote.PlatformServiceFee + subscription.OutletCommissionAmount
            });
        });

        return MapSubscription(subscription, "Pending");
    }

    public async Task<SubscriptionDto?> AcceptAsync(Guid subscriptionId)
    {
        if (current.UserId is not Guid customerId)
            return null;

        var subscription = await subscriptions.GetAsync(subscriptionId)
            ?? throw new KeyNotFoundException("Package not found.");
        if (!subscription.IsOutletCreated || subscription.CustomerId != customerId)
            throw new UnauthorizedAccessException("This package is not available to the current customer.");
        if (current.OutletId is not Guid customerOutletId || subscription.OutletId != customerOutletId)
            throw new UnauthorizedAccessException("This package belongs to a different outlet.");
        if (subscription.PackageStatus == "Active")
            return MapSubscription(subscription, (await payments.GetLatestBySubscriptionAsync(subscription.Id))?.Status ?? "Paid");
        if (subscription.PackageStatus != "SentToCustomer")
            throw new InvalidOperationException("This package has already been accepted or is no longer available.");

        subscription.PackageStatus = "PaymentPending";
        subscription.AcceptedAtUtc = DateTime.UtcNow;
        await subscriptions.UpdateAsync(subscription);
        return MapSubscription(subscription, "Pending");
    }

    public async Task<SubscriptionDto?> MarkPaidAsync(Guid subscriptionId, MarkOutletPackagePaidRequest request)
    {
        if (current.OutletId is not Guid outletId || current.UserId is not Guid outletUserId)
            return null;

        var subscription = await subscriptions.GetAsync(subscriptionId)
            ?? throw new KeyNotFoundException("Package not found.");
        if (!subscription.IsOutletCreated || subscription.OutletId != outletId)
            throw new UnauthorizedAccessException("Package does not belong to this outlet.");
        if (subscription.PackageStatus == "Active")
            return MapSubscription(subscription, "Paid");

        var existing = await payments.GetLatestBySubscriptionAsync(subscription.Id);
        if (existing?.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true)
            return MapSubscription(await activation.ActivateAsync(subscription.Id, existing.Provider, outletUserId), "Paid");

        var method = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Cash" : request.PaymentMethod.Trim();
        var allowedMethods = new[] { "Cash", "UPI", "BankTransfer", "Manual" };
        if (!allowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Payment method must be Cash, UPI, BankTransfer or Manual.");
        method = allowedMethods.First(x => x.Equals(method, StringComparison.OrdinalIgnoreCase));
        var now = DateTime.UtcNow;
        await payments.AddAsync(new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = subscription.CustomerId,
            SubscriptionId = subscription.Id,
            Provider = "OutletManual",
            ProviderPaymentId = $"outlet_{Guid.NewGuid():N}",
            IdempotencyKey = $"outlet-manual-{subscription.Id}-{Guid.NewGuid():N}",
            Amount = subscription.TotalCharged,
            Currency = "INR",
            Status = "Paid",
            CreatedAtUtc = now,
            PaidAtUtc = now
        });

        subscription.PaymentMethod = method;
        subscription.PaidAtUtc = now;
        await subscriptions.UpdateAsync(subscription);
        var activated = await activation.ActivateAsync(subscription.Id, method, outletUserId);
        return MapSubscription(activated, "Paid");
    }

    private async Task<OutletPackageQuoteDto?> BuildQuoteAsync(OutletPackageQuoteRequest request)
    {
        if (current.OutletId is not Guid outletId)
            return null;
        if (request.CustomerId == Guid.Empty)
            throw new ArgumentException("Select a customer.");
        if (request.Selections is null || request.Selections.Count == 0)
            throw new ArgumentException("Select at least one meal.");

        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        if (outlet.Status != OutletStatus.Live)
            throw new InvalidOperationException("Outlet is not live yet. Complete outlet setup before creating customer packages.");
        if (request.OutletId != outletId)
            throw new UnauthorizedAccessException("The package outlet does not match the current outlet.");

        var customer = await users.FindByIdAsync(request.CustomerId) ?? throw new KeyNotFoundException("Customer not found.");
        if (customer.Role != UserRole.Customer || !customer.IsActive)
            throw new InvalidOperationException("Select an active customer.");

        await EnsureCustomerAccessAsync(customer.Id);

        var duration = Parse<SubscriptionDuration>(request.Duration, "package duration");
        var deliveryMode = Parse<SubscriptionDeliveryMode>(request.DeliveryMode, "delivery mode");
        var city = string.IsNullOrWhiteSpace(request.DeliveryCity) ? outlet.City : request.DeliveryCity.Trim();
        if (!city.Equals(outlet.City, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"This outlet only serves {outlet.City}.");

        var mealRows = await BuildSelectionsAsync(request.Selections, outletId, outlet, customer.Id, deliveryMode);
        ValidateWindow(duration, mealRows);
        if (deliveryMode == SubscriptionDeliveryMode.OneDeliveryPerDay)
        {
            foreach (var day in mealRows.GroupBy(x => x.MealDate.Date))
            {
                if (day.Select(x => x.AddressId).Distinct().Count() > 1)
                    throw new ArgumentException("One delivery per day requires the same address for all meals on the same day.");
            }
        }

        var selectedRecipes = mealRows.Select(x => x.RecipeId).Distinct().ToList();
        var recipeEntities = await recipes.GetByIdsAsync(selectedRecipes);
        var allergyWarnings = await allergySafety.GetWarningsAsync(customer.Id, recipeEntities);
        var confirmed = request.ConfirmedAllergyRecipeIds ?? [];
        var requiresAllergyConfirmation = allergyWarnings.Any(x => !confirmed.Contains(x.RecipeId));

        var addressIds = mealRows.Select(x => x.AddressId).Distinct().ToList();
        if (addressIds.Any(x => !x.HasValue))
            throw new ArgumentException("Every scheduled meal requires a delivery address.");

        var deliveryCity = city;
        foreach (var addressId in addressIds.Select(x => x!.Value))
        {
            var address = await addresses.GetAsync(customer.Id, addressId)
                ?? throw new KeyNotFoundException("One or more customer delivery addresses were not found.");
            if (!address.City.Equals(deliveryCity, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Address {address.Label} is in {address.City}, but this package is for {deliveryCity}.");
        }

        var gross = Math.Round(mealRows.Sum(x => x.MealPrice), 2);
        var discountType = Parse<OutletPackageDiscountType>(request.DiscountType, "discount type");
        if (request.DiscountValue < 0)
            throw new ArgumentException("Discount cannot be negative.");
        if (discountType == OutletPackageDiscountType.Percent && request.DiscountValue > 100)
            throw new ArgumentException("Percentage discount cannot exceed 100%.");

        var discountAmount = discountType switch
        {
            OutletPackageDiscountType.Percent => Math.Round(gross * request.DiscountValue / 100m, 2),
            OutletPackageDiscountType.Fixed => Math.Round(request.DiscountValue, 2),
            _ => 0m
        };
        discountAmount = Math.Min(gross, Math.Max(0m, discountAmount));
        if (discountAmount > 0m && string.IsNullOrWhiteSpace(request.DiscountReason))
            throw new ArgumentException("A reason is required when applying a negotiated outlet discount.");
        var discountedMealAmount = Math.Round(gross - discountAmount, 2);

        var delivery = await CalculateDeliveryAsync(outletId, deliveryMode, mealRows, customer.Id);
        var service = platformFee.Calculate(discountedMealAmount);
        var taxes = taxStrategy.Calculate(discountedMealAmount, service, outlet.RestaurantGstRate, outlet.RestaurantGstMode);
        var commissionRate = await GetCommissionRateAsync(outletId);
        var commission = Math.Round(taxes.RestaurantTaxableAmount * commissionRate, 2);
        var total = Math.Round(taxes.RestaurantTaxableAmount + taxes.RestaurantAmount + delivery + service + taxes.PlatformAmount, 2);
        var outletSettlement = Math.Round(taxes.RestaurantTaxableAmount + taxes.RestaurantAmount - commission, 2);

        var quotes = new List<DeliveryQuoteDto>();
        foreach (var addressId in addressIds.Select(x => x!.Value))
            quotes.Add(await deliveryCalculator.QuoteAsync(outletId, customer.Id, addressId));

        var discountPercent = gross == 0 ? 0 : Math.Round(discountAmount / gross * 100m, 4);
        return new OutletPackageQuoteDto(
            customer.Id,
            gross,
            discountAmount,
            discountPercent,
            discountType.ToString(),
            taxes.RestaurantTaxableAmount,
            taxes.RestaurantAmount,
            taxes.RestaurantRate,
            taxes.RestaurantMode.ToString(),
            delivery,
            service,
            taxes.PlatformAmount,
            commission,
            outletSettlement,
            total,
            quotes,
            allergyWarnings,
            requiresAllergyConfirmation);
    }

    private async Task<List<SubscriptionMealSelection>> BuildSelectionsAsync(
        IReadOnlyList<MealSelectionItem> items,
        Guid outletId,
        Outlet outlet,
        Guid customerId,
        SubscriptionDeliveryMode deliveryMode)
    {
        var rs = (await recipes.GetByOutletAsync(outletId)).Where(x => x.IsActive).ToDictionary(x => x.Id);
        var outletMenu = await menu.GetByOutletAsync(outletId);
        var result = new List<SubscriptionMealSelection>();

        foreach (var item in items)
        {
            if (!rs.TryGetValue(item.RecipeId, out var recipe))
                throw new ArgumentException("One or more selected meals are not available from this outlet.");
            if (!Enum.IsDefined(typeof(MealSlot), item.MealSlot))
                throw new ArgumentException("Invalid meal slot.");
            var slot = (MealSlot)item.MealSlot;
            if (!outletMenu.Any(x => x.DayOfWeek == item.MealDate.Date.DayOfWeek && x.MealSlot == slot && x.RecipeId == item.RecipeId && x.IsAvailable))
                throw new ArgumentException($"{recipe.Name} is not available on {item.MealDate:dddd} at the selected slot.");
            var portion = Enum.IsDefined(typeof(MealPortionSize), item.PortionSize)
                ? (MealPortionSize)item.PortionSize
                : MealPortionSize.Regular;
            result.Add(new SubscriptionMealSelection
            {
                Id = Guid.NewGuid(),
                SubscriptionId = Guid.Empty,
                MealDate = item.MealDate.Date,
                MealSlot = slot,
                RecipeId = item.RecipeId,
                PortionSize = portion,
                Status = MealSelectionStatus.Scheduled,
                MealPrice = mealPrice.GetPrice(recipe, portion),
                AddressId = item.AddressId
            });
        }

        var duplicate = result.GroupBy(x => new { x.MealDate, x.MealSlot }).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException("A package can contain only one meal per day and meal slot.");

        return result;
    }

    private static void ValidateWindow(SubscriptionDuration duration, IReadOnlyList<SubscriptionMealSelection> meals)
    {
        var start = meals.Min(x => x.MealDate).Date;
        if (start < DateTime.UtcNow.Date)
            throw new ArgumentException("Package start date cannot be in the past.");
        var days = duration switch
        {
            SubscriptionDuration.ThreeDays => 3,
            SubscriptionDuration.FiveDays => 5,
            SubscriptionDuration.OneWeek => 7,
            SubscriptionDuration.TwoWeeks => 14,
            SubscriptionDuration.OneMonth => 28,
            _ => 1
        };
        var end = start.AddDays(days - 1);
        if (meals.Any(x => x.MealDate.Date < start || x.MealDate.Date > end))
            throw new ArgumentException("Selected meals are outside the package duration.");
    }

    private async Task<decimal> CalculateDeliveryAsync(
        Guid outletId,
        SubscriptionDeliveryMode mode,
        IReadOnlyList<SubscriptionMealSelection> meals,
        Guid customerId)
    {
        var total = 0m;
        var groups = mode == SubscriptionDeliveryMode.OneDeliveryPerDay
            ? meals.GroupBy(x => x.MealDate.Date).Select(g => g.ToList())
            : meals.Select(x => new List<SubscriptionMealSelection> { x }).ToList();

        foreach (var group in groups)
        {
            var first = group[0];
            if (!first.AddressId.HasValue)
                throw new ArgumentException("Every scheduled meal requires an address.");
            var quote = await deliveryCalculator.QuoteAsync(outletId, customerId, first.AddressId.Value);
            foreach (var row in group)
                row.DeliveryFee = quote.DeliveryFee;
            total += quote.DeliveryFee;
        }
        return total;
    }

    private async Task<decimal> GetCommissionRateAsync(Guid outletId)
    {
        var currentSubscription = await outletSubscriptions.GetByOutletAsync(outletId);
        return (currentSubscription?.TransactionFeePercent ?? 0m) / 100m;
    }

    private async Task EnsureCustomerAccessAsync(Guid customerId)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("Outlet association is required.");

        var user = await users.FindByIdAsync(customerId);
        if (user is null || user.Role != UserRole.Customer)
            throw new KeyNotFoundException("Customer not found.");

        if (user.OutletId != outletId)
            throw new UnauthorizedAccessException("Customer is not associated with this outlet.");
    }

    private static SubscriptionDto MapSubscription(Subscription x, string paymentStatus)
    {
        return new SubscriptionDto(
            x.Id,
            x.CustomerId,
            x.OutletId,
            x.MealPlanId,
            x.PlanName,
            x.DeliveryMode.ToString(),
            x.Price,
            x.DeliveryFee,
            x.CustomerTransactionFeePercent,
            x.TransactionFee,
            x.TotalCharged,
            x.OutletAmount,
            x.Frequency,
            x.MealsPerDay,
            x.MealsPerWeek,
            x.Status.ToString(),
            x.NextDeliveryDate,
            0,
            paymentStatus,
            x.DeliveryCity,
            x.GrossMealAmount,
            x.SubscriptionDiscountAmount,
            x.RestaurantTaxableAmount,
            x.RestaurantGstAmount,
            x.RestaurantGstRate,
            x.RestaurantGstMode.ToString(),
            x.PlatformServiceFee,
            x.PlatformServiceGst,
            x.PackageStatus,
            x.IsOutletCreated,
            x.OutletDiscountType.ToString(),
            x.OutletDiscountValue,
            x.OutletDiscountReason);
    }

    private static UserDto MapUser(User x)
        => new(x.Id, x.Email, x.FirstName, x.LastName, x.Role.ToString(), x.OutletId);

    private static T Parse<T>(string value, string label) where T : struct, Enum
        => Enum.TryParse<T>(value, true, out var result)
            ? result
            : throw new ArgumentException($"Invalid {label}.");
}
