using System.Security.Cryptography;
using System.Text;
using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;
using Microsoft.Extensions.Configuration;

namespace HealthApp.Application.Services;

public sealed class OutletDemoService(
    IUserRepository users,
    IOutletRepository outlets,
    ISaaSPlanRepository plans,
    IOutletSubscriptionRepository outletSubscriptions,
    IMealPlanRepository mealPlans,
    IRecipeRepository recipes,
    IOutletMenuRepository menu,
    IServiceCityRepository serviceCities,
    ICityAreaRepository cityAreas,
    IOutletDeliveryAreaRepository outletDeliveryAreas,
    IDeliveryPricingRepository deliveryPricing,
    IOutletLegalPolicyRepository legalPolicies,
    ICustomerAddressRepository customerAddresses,
    ISubscriptionRepository subscriptions,
    ISubscriptionMealSelectionRepository mealSelections,
    IOrderRepository orders,
    IDeliveryRepository deliveries,
    IPasswordService passwords,
    IConfiguration configuration,
    ITransactionalEmailService emails)
    : IOutletDemoService
{
    private static readonly char[] PasswordChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789".ToCharArray();

    public async Task<OutletDemoRequestDto> RequestAsync(RequestOutletDemoRequest request, CancellationToken cancellationToken = default)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Enter a valid email address.");

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (existing.IsDemo && existing.DemoExpiresAtUtc.HasValue && existing.DemoExpiresAtUtc.Value > DateTime.UtcNow)
                throw new InvalidOperationException("A live demo already exists for this email address.");
            throw new InvalidOperationException("An account already exists for this email address. Please use another email for the demo.");
        }

        var city = await serviceCities.GetByCityAsync("Bengaluru")
            ?? throw new InvalidOperationException("Demo city is not available.");

        var demoExpires = DateTime.UtcNow.AddDays(7);
        var password = GeneratePassword();
        var outletId = Guid.NewGuid();
        var slug = await CreateUniqueSlugAsync($"healthapp-demo-{Guid.NewGuid():N}"[..24]);
        var outlet = new Outlet
        {
            Id = outletId,
            Name = string.IsNullOrWhiteSpace(request.BusinessName) ? "HealthApp Demo Kitchen" : $"{request.BusinessName.Trim()} · Demo",
            Slug = slug,
            Subdomain = slug,
            City = city.City,
            State = city.State,
            Pincode = "560001",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            ServiceRadiusKm = 20,
            Status = OutletStatus.Active,
            BillingPlan = BillingPlan.Starter,
            About = "HealthApp demo workspace for exploring subscriptions, kitchen operations, deliveries and customers.",
            HealthHighlights = "Subscriptions,Kitchen Planning,Delivery Management,Customer Management"
        };
        outlet.DeliveryDays = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday";
        outlet.RestaurantGstRate = 5m;
        outlet.RestaurantGstMode = GstMode.Exclusive;
        outlet.LegalVersion = "1.0-demo";
        outlet.LegalEffectiveDateUtc = DateTime.UtcNow;
        outlet.LegalPoliciesPublished = true;
        outlet.CustomerTermsAndConditions = DemoLegalPolicyTemplates.Terms(outlet.Name);
        outlet.CustomerPrivacyPolicy = DemoLegalPolicyTemplates.Privacy(outlet.Name);
        outlet.CancellationRefundPolicy = DemoLegalPolicyTemplates.Cancellation(outlet.Name);
        outlet.MealSkipReschedulePolicy = DemoLegalPolicyTemplates.SkipReschedule(outlet.Name);
        outlet.DeliveryPolicy = DemoLegalPolicyTemplates.Delivery(outlet.Name);
        outlet.AllergenDietaryDisclaimer = DemoLegalPolicyTemplates.Allergen(outlet.Name);
        outlet.PaymentPricingPromotionalTerms = DemoLegalPolicyTemplates.Payment(outlet.Name);

        await outlets.AddAsync(outlet);

        var legalCanonical = string.Join("\n---\n", new[]
        {
            outlet.CustomerTermsAndConditions,
            outlet.CustomerPrivacyPolicy,
            outlet.CancellationRefundPolicy,
            outlet.MealSkipReschedulePolicy,
            outlet.DeliveryPolicy,
            outlet.AllergenDietaryDisclaimer,
            outlet.PaymentPricingPromotionalTerms
        });
        await legalPolicies.PublishVersionAsync(new OutletLegalPolicyVersion
        {
            Id = Guid.NewGuid(),
            OutletId = outlet.Id,
            Version = outlet.LegalVersion,
            CustomerTermsAndConditions = outlet.CustomerTermsAndConditions,
            CustomerPrivacyPolicy = outlet.CustomerPrivacyPolicy,
            CancellationRefundPolicy = outlet.CancellationRefundPolicy,
            MealSkipReschedulePolicy = outlet.MealSkipReschedulePolicy,
            DeliveryPolicy = outlet.DeliveryPolicy,
            AllergenDietaryDisclaimer = outlet.AllergenDietaryDisclaimer,
            PaymentPricingPromotionalTerms = outlet.PaymentPricingPromotionalTerms,
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legalCanonical))).ToLowerInvariant(),
            EffectiveDateUtc = outlet.LegalEffectiveDateUtc ?? DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            PublishedAtUtc = DateTime.UtcNow,
            IsPublished = true
        });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwords.Hash(password),
            FirstName = "Demo",
            LastName = "Outlet",
            Role = UserRole.OutletAdmin,
            OutletId = outlet.Id,
            IsActive = true,
            IsDemo = true,
            DemoExpiresAtUtc = demoExpires
        };
        await users.AddAsync(user);

        var free = (await plans.GetActiveAsync()).FirstOrDefault(x => x.Name.Equals("Free", StringComparison.OrdinalIgnoreCase))
            ?? (await plans.GetActiveAsync()).FirstOrDefault()
            ?? throw new InvalidOperationException("No active subscription plan is available.");

        await outletSubscriptions.AddAsync(new OutletSubscription
        {
            Id = Guid.NewGuid(),
            OutletId = outlet.Id,
            SaaSPlanId = free.Id,
            BillingCycle = "Monthly",
            SubscriptionFee = 0,
            SetupFee = 0,
            TransactionFeePercent = free.CustomerTransactionFeePercent,
            StartDate = DateTime.UtcNow.Date,
            RenewalDate = demoExpires.Date,
            Status = "Demo"
        });

        await SeedDemoWorkspaceAsync(outlet.Id, city.City, cancellationToken);

        var portalUrl = configuration["Demo:OutletPortalUrl"] ?? "http://localhost:5174";
        var demoPortalUrl = $"{portalUrl}{(portalUrl.Contains('?') ? "&" : "?")}demo={Uri.EscapeDataString(outlet.Slug)}";
        await emails.SendAsync(
            EmailTemplateId.OutletDemoAccess,
            email,
            new Dictionary<string, string?>
            {
                ["FirstName"] = user.FirstName,
                ["Email"] = email,
                ["Password"] = password,
                ["PortalUrl"] = demoPortalUrl,
                ["ExpiresAtUtc"] = $"{demoExpires:dd MMM yyyy HH:mm} UTC"
            },
            cancellationToken);

        return new OutletDemoRequestDto(true, "Your 7-day demo account has been created. Login details have been sent to your email address.", demoExpires);
    }

    private async Task SeedDemoWorkspaceAsync(Guid outletId, string city, CancellationToken ct)
    {
        var plan = new MealPlan
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            Name = "Demo Healthy Weekly",
            Frequency = "Weekly",
            MealsPerDay = 2,
            MealsPerWeek = 14,
            Price = 2499,
            Currency = "INR",
            Description = "A realistic demo subscription plan for exploring the full customer journey."
        };
        await mealPlans.AddAsync(plan);

        var recipesToAdd = new[]
        {
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Grilled Chicken Bowl", MealType="Meal", Calories=540, ProteinGrams=42, CarbsGrams=48, FatGrams=18, FiberGrams=8, Category=RecipeCategory.NonVeg, PricePerMeal=220, LargePricePerMeal=260, Description="Grilled chicken, rice and seasonal vegetables.", Tags="high-protein,demo" },
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Paneer Power Bowl", MealType="Meal", Calories=510, ProteinGrams=30, CarbsGrams=46, FatGrams=20, FiberGrams=7, Category=RecipeCategory.Veg, PricePerMeal=190, LargePricePerMeal=230, Description="Paneer with grains, greens and vegetables.", Tags="vegetarian,demo" },
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Chickpea Buddha Bowl", MealType="Meal", Calories=470, ProteinGrams=20, CarbsGrams=55, FatGrams=14, FiberGrams=10, Category=RecipeCategory.Vegan, PricePerMeal=170, LargePricePerMeal=210, Description="Chickpeas, grains, greens and tahini.", Tags="vegan,demo" },
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Mango Ginger Juice", MealType="Juice", Calories=120, ProteinGrams=1, CarbsGrams=28, FatGrams=0, FiberGrams=2, Category=RecipeCategory.Veg, PricePerMeal=65, LargePricePerMeal=80, Description="Fresh mango and ginger juice.", Tags="juice,demo" },
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Cucumber Mint Cooler", MealType="Juice", Calories=45, ProteinGrams=1, CarbsGrams=10, FatGrams=0, FiberGrams=1, Category=RecipeCategory.Vegan, PricePerMeal=55, LargePricePerMeal=70, Description="Cucumber, mint and lime cooler.", Tags="juice,demo" },
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Roasted Protein Bites", MealType="Snack", Calories=180, ProteinGrams=9, CarbsGrams=15, FatGrams=8, FiberGrams=3, Category=RecipeCategory.Veg, PricePerMeal=75, LargePricePerMeal=90, Description="Roasted seed and lentil protein bites.", Tags="snack,demo" },
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Plain Greek Curd", MealType="Curd", Calories=110, ProteinGrams=7, CarbsGrams=8, FatGrams=5, FiberGrams=0, Category=RecipeCategory.Veg, PricePerMeal=50, LargePricePerMeal=60, Description="Cooling plain curd portion.", Tags="curd,demo" }
        };
        foreach (var recipe in recipesToAdd)
            await recipes.AddAsync(recipe);

        var main = recipesToAdd.Where(x => x.MealType == "Meal").ToArray();
        var juice = recipesToAdd.Where(x => x.MealType == "Juice").ToArray();
        var snack = recipesToAdd.Where(x => x.MealType == "Snack").ToArray();
        var curd = recipesToAdd.Where(x => x.MealType == "Curd").ToArray();

        var days = Enum.GetValues<DayOfWeek>();
        foreach (var day in days)
        {
            await menu.AddAsync(new OutletMenuItem { Id=Guid.NewGuid(), OutletId=outletId, RecipeId=main[(int)day % main.Length].Id, DayOfWeek=day, MealSlot=MealSlot.Morning, IsAvailable=true, DisplayOrder=1, OptionGroup="Meal", IsRequired=true, MaxSelections=1 });
            await menu.AddAsync(new OutletMenuItem { Id=Guid.NewGuid(), OutletId=outletId, RecipeId=juice[(int)day % juice.Length].Id, DayOfWeek=day, MealSlot=MealSlot.Morning, IsAvailable=true, DisplayOrder=2, OptionGroup="Juice", IsRequired=false, MaxSelections=1 });
            await menu.AddAsync(new OutletMenuItem { Id=Guid.NewGuid(), OutletId=outletId, RecipeId=snack[0].Id, DayOfWeek=day, MealSlot=MealSlot.Morning, IsAvailable=true, DisplayOrder=3, OptionGroup="Snack", IsRequired=false, MaxSelections=1 });
            await menu.AddAsync(new OutletMenuItem { Id=Guid.NewGuid(), OutletId=outletId, RecipeId=curd[0].Id, DayOfWeek=day, MealSlot=MealSlot.Afternoon, IsAvailable=true, DisplayOrder=1, OptionGroup="Curd", IsRequired=false, MaxSelections=1 });
            await menu.AddAsync(new OutletMenuItem { Id=Guid.NewGuid(), OutletId=outletId, RecipeId=main[(int)day % main.Length].Id, DayOfWeek=day, MealSlot=MealSlot.Evening, IsAvailable=true, DisplayOrder=1, OptionGroup="Meal", IsRequired=true, MaxSelections=1 });
        }

        foreach (var max in new[] { (km:2m,fee:10m),(km:5m,fee:20m),(km:10m,fee:30m),(km:20m,fee:70m) })
            await deliveryPricing.AddAsync(new DeliveryPricingRule { Id=Guid.NewGuid(), OutletId=outletId, MaxDistanceKm=max.km, Fee=max.fee });

        var areas = await cityAreas.GetActiveAsync(city);
        if (areas.Count > 0)
            await outletDeliveryAreas.ReplaceAsync(outletId, areas.Take(Math.Min(5, areas.Count)).Select(x => new OutletDeliveryArea { Id=Guid.NewGuid(), OutletId=outletId, CityAreaId=x.Id }));

        // Seed real-looking operating data so the demo opens on meaningful dashboards
        // instead of an empty workspace.
        var demoCustomers = new[]
        {
            new { First="Anita", Last="Sharma", Email="anita.demo@fitfood.example", Phone="+919876543201", AreaIndex=0 },
            new { First="Rahul", Last="Mehta", Email="rahul.demo@fitfood.example", Phone="+919876543202", AreaIndex=1 },
            new { First="Priya", Last="Nair", Email="priya.demo@fitfood.example", Phone="+919876543203", AreaIndex=2 }
        };

        var demoCustomerUsers = new List<User>();
        foreach (var customer in demoCustomers)
        {
            var customerId = Guid.NewGuid();
            var customerUser = new User
            {
                Id=customerId,
                Email=customer.Email,
                PasswordHash=passwords.Hash("demo"),
                FirstName=customer.First,
                LastName=customer.Last,
                MobileNumber=customer.Phone,
                Role=UserRole.Customer,
                OutletId=outletId,
                IsActive=true,
                IsDemo=true,
                DemoExpiresAtUtc=DateTime.UtcNow.AddDays(7)
            };
            await users.AddAsync(customerUser);
            demoCustomerUsers.Add(customerUser);

            var area = areas.Count > 0 ? areas[Math.Min(customer.AreaIndex, areas.Count-1)] : null;
            var address = new CustomerAddress
            {
                Id=Guid.NewGuid(),
                CustomerId=customerId,
                CityAreaId=area?.Id,
                City=city,
                State="Karnataka",
                Pincode=area?.Pincode ?? "560001",
                Locality=area?.Name ?? "Central Bengaluru",
                Label="Home",
                AddressLine1=customer.AreaIndex==0?"24 MG Road":customer.AreaIndex==1?"18 Indiranagar 100ft Road":"8 Koramangala 5th Block",
                AddressLine2="Bengaluru",
                ContactName=$"{customer.First} {customer.Last}",
                ContactPhone=customer.Phone,
                Latitude=(area?.Latitude ?? 12.9716)+(customer.AreaIndex*0.003),
                Longitude=(area?.Longitude ?? 77.5946)+(customer.AreaIndex*0.003),
                IsDefault=true
            };
            await customerAddresses.AddAsync(address);
        }

        // Two drivers make delivery routing immediately demonstrable.
        foreach (var driver in new[]
        {
            new { First="Arjun", Last="Driver", Email="driver.arjun@fitfood.example", Phone="+919876543211" },
            new { First="Kiran", Last="Driver", Email="driver.kiran@fitfood.example", Phone="+919876543212" }
        })
        {
            await users.AddAsync(new User
            {
                Id=Guid.NewGuid(),
                Email=driver.Email,
                PasswordHash=passwords.Hash("demo"),
                FirstName=driver.First,
                LastName=driver.Last,
                MobileNumber=driver.Phone,
                Role=UserRole.Driver,
                OutletId=outletId,
                IsActive=true,
                IsDemo=true,
                DemoExpiresAtUtc=DateTime.UtcNow.AddDays(7)
            });
        }

        var customerRows = demoCustomerUsers;
        var firstCustomer = customerRows[0];
        var firstAddress = await customerAddresses.GetByCustomerAsync(firstCustomer.Id);
        var primaryAddress = firstAddress.FirstOrDefault();

        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(6);
        var subscription = new Subscription
        {
            Id=Guid.NewGuid(),
            CustomerId=firstCustomer.Id,
            OutletId=outletId,
            DeliveryCity=city,
            MealPlanId=plan.Id,
            PlanName=plan.Name,
            DeliveryMode=SubscriptionDeliveryMode.OneDeliveryPerDay,
            Duration=SubscriptionDuration.OneWeek,
            StartDate=startDate,
            EndDate=endDate,
            GrossMealAmount=2499,
            SubscriptionDiscountPercent=5,
            SubscriptionDiscountAmount=124.95m,
            NetMealAmount=2374.05m,
            PlatformServiceFee=71.22m,
            PlatformServiceGst=12.82m,
            RestaurantGstRate=5m,
            RestaurantGstMode=GstMode.Exclusive,
            RestaurantTaxableAmount=2374.05m,
            RestaurantGstAmount=118.70m,
            LateSkipFee=0,
            Price=2499,
            DeliveryFee=70,
            CustomerTransactionFeePercent=3,
            TransactionFee=71.22m,
            TotalCharged=2633.99m,
            OutletAmount=2492.77m,
            PlatformServiceFeePercent=3,
            PlatformServiceGstRate=18,
            OutletCommissionPercent=0,
            OutletCommissionAmount=0,
            TotalMealCount=14,
            Frequency="Weekly",
            MealsPerDay=2,
            MealsPerWeek=14,
            Status=SubscriptionStatus.Active,
            PackageStatus="Active",
            IsPreplanned=true,
            PricingMode="Calculated",
            PriceVisibleToCustomer=true,
            DeliveryFeeVisibleToCustomer=true,
            IsOutletCreated=false,
            PaymentMethod="Demo",
            PaidAtUtc=DateTime.UtcNow,
            NextDeliveryDate=startDate
        };
        await subscriptions.AddAsync(subscription);

        var selections = new List<SubscriptionMealSelection>();
        for (var i=0;i<5;i++)
        {
            var mealDate=startDate.AddDays(i);
            var mealRecipe=main[i%main.Length];
            selections.Add(new SubscriptionMealSelection
            {
                Id=Guid.NewGuid(),SubscriptionId=subscription.Id,MealDate=mealDate,MealSlot=MealSlot.Afternoon,
                RecipeId=mealRecipe.Id,AddressId=primaryAddress?.Id,PortionSize=MealPortionSize.Regular,
                Status=i==0?MealSelectionStatus.Prepared:MealSelectionStatus.Scheduled,
                MealPrice=mealRecipe.PricePerMeal,DeliveryFee=70
            });
            var side=juice[i%juice.Length];
            selections.Add(new SubscriptionMealSelection
            {
                Id=Guid.NewGuid(),SubscriptionId=subscription.Id,MealDate=mealDate,MealSlot=MealSlot.Afternoon,
                RecipeId=side.Id,AddressId=primaryAddress?.Id,PortionSize=MealPortionSize.Regular,
                Status=MealSelectionStatus.Scheduled,MealPrice=side.PricePerMeal,DeliveryFee=70
            });
        }
        await mealSelections.AddRangeAsync(selections);

        foreach(var selectionGroup in selections.GroupBy(x=>x.MealDate))
        {
            var selection=selectionGroup.First();
            var order=new Order
            {
                Id=Guid.NewGuid(),CustomerId=firstCustomer.Id,OutletId=outletId,SubscriptionId=subscription.Id,
                Total=subscription.TotalCharged/5m,Status=selection.MealDate==startDate?OrderStatus.Preparing:OrderStatus.Confirmed,
                DeliveryDate=selectionGroup.Key,Address=primaryAddress is null?city:$"{primaryAddress.AddressLine1}, {primaryAddress.AddressLine2}",
                DeliveryAddressId=primaryAddress?.Id
            };
            await orders.AddAsync(order);
            await deliveries.AddAsync(new Delivery
            {
                Id=Guid.NewGuid(),OrderId=order.Id,SubscriptionId=subscription.Id,OutletId=outletId,CustomerId=firstCustomer.Id,
                DeliveryAddressId=primaryAddress?.Id,ScheduledDate=selectionGroup.Key,MealSlot=MealSlot.Afternoon,
                CustomerName=$"{firstCustomer.FirstName} {firstCustomer.LastName}",Address=order.Address,DeliveryFee=70,
                Status=selection.MealDate==startDate?DeliveryStatus.Preparing:DeliveryStatus.Scheduled
            });
        }
    }

    private async Task<string> CreateUniqueSlugAsync(string baseSlug)
    {
        var slug = baseSlug;
        var counter = 2;
        while (await outlets.GetBySlugAsync(slug) is not null)
            slug = $"{baseSlug}-{counter++}";
        return slug;
    }

    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        var chars = new char[12];
        for (var i=0;i<chars.Length;i++) chars[i]=PasswordChars[bytes[i]%PasswordChars.Length];
        return new string(chars);
    }
}
