using System.Security.Cryptography;
using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

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
    IPasswordService passwords,
    IEmailService email)
    : IOutletDemoService
{
    private static readonly char[] PasswordChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%".ToCharArray();

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
        await outlets.AddAsync(outlet);

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

        var portalUrl = "http://localhost:5175";
        var body = $"Hello,\n\nYour HealthApp outlet demo account is ready.\n\nLogin: {email}\nPassword: {password}\nDemo portal: {portalUrl}\nValid until: {demoExpires:dd MMM yyyy HH:mm} UTC\n\nDuring the 7-day demo you can create customers, build subscriptions, add drivers, manage recipes and menus, review kitchen orders and explore delivery planning.\n\nThis is a demo account. No subscription payment is required and access is automatically blocked after the expiry date.\n\nRegards,\nHealthApp";
        await email.SendAsync(email, "Your HealthApp outlet demo account", body, cancellationToken);

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
            Description = "Demo subscription plan."
        };
        await mealPlans.AddAsync(plan);

        var recipesToAdd = new[]
        {
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Grilled Chicken Bowl", Calories=540, ProteinGrams=42, CarbsGrams=48, FatGrams=18, FiberGrams=8, Category=RecipeCategory.NonVeg, PricePerMeal=220, LargePricePerMeal=260, Description="Grilled chicken, rice and seasonal vegetables." },
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Paneer Power Bowl", Calories=510, ProteinGrams=30, CarbsGrams=46, FatGrams=20, FiberGrams=7, Category=RecipeCategory.Veg, PricePerMeal=190, LargePricePerMeal=230, Description="Paneer with grains, greens and vegetables." },
            new Recipe { Id=Guid.NewGuid(), OutletId=outletId, Name="Chickpea Buddha Bowl", Calories=470, ProteinGrams=20, CarbsGrams=55, FatGrams=14, FiberGrams=10, Category=RecipeCategory.Vegan, PricePerMeal=170, LargePricePerMeal=210, Description="Chickpeas, grains, greens and tahini." }
        };
        foreach (var recipe in recipesToAdd)
            await recipes.AddAsync(recipe);

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var day in days)
        {
            await menu.AddAsync(new OutletMenuItem { Id=Guid.NewGuid(), OutletId=outletId, RecipeId=recipesToAdd[0].Id, DayOfWeek=day, MealSlot=MealSlot.Afternoon, IsAvailable=true, DisplayOrder=1 });
            await menu.AddAsync(new OutletMenuItem { Id=Guid.NewGuid(), OutletId=outletId, RecipeId=recipesToAdd[1].Id, DayOfWeek=day, MealSlot=MealSlot.Evening, IsAvailable=true, DisplayOrder=1 });
            await menu.AddAsync(new OutletMenuItem { Id=Guid.NewGuid(), OutletId=outletId, RecipeId=recipesToAdd[2].Id, DayOfWeek=day, MealSlot=MealSlot.Morning, IsAvailable=true, DisplayOrder=1 });
        }

        foreach (var max in new[] { (km:2m,fee:10m), (km:5m,fee:20m), (km:10m,fee:30m), (km:20m,fee:70m) })
            await deliveryPricing.AddAsync(new DeliveryPricingRule { Id=Guid.NewGuid(), OutletId=outletId, MaxDistanceKm=max.km, Fee=max.fee });

        var areas = await cityAreas.GetActiveAsync(city);
        if (areas.Count > 0)
            await outletDeliveryAreas.ReplaceAsync(outletId, areas.Take(5).Select(x => new OutletDeliveryArea { Id=Guid.NewGuid(), OutletId=outletId, CityAreaId=x.Id }));

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
