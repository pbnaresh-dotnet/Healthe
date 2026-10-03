using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(HealthAppDbContext db, IPasswordService passwords, CancellationToken ct = default)
    {
        await SeedCatalogAsync(db, ct);
        if (await db.Outlets.AnyAsync(ct))
        {
            await EnsureExistingRecipeCatalogLinksAsync(db, ct);
            return;
        }

        var free = new SaaSPlan { Id=Guid.NewGuid(), Name="Free", MonthlyFee=0, AnnualFee=0, IncludedActiveCustomers=10, AdditionalCustomerFee=0, CustomerTransactionFeePercent=3m, Description="Free plan for up to 10 active customers." };
        var basic = new SaaSPlan { Id=Guid.NewGuid(), Name="Basic", MonthlyFee=999, AnnualFee=9990, IncludedActiveCustomers=50, AdditionalCustomerFee=15, CustomerTransactionFeePercent=2m, Description="For small meal businesses." };
        var growth = new SaaSPlan { Id=Guid.NewGuid(), Name="Growth", MonthlyFee=2499, AnnualFee=24990, IncludedActiveCustomers=150, AdditionalCustomerFee=12, CustomerTransactionFeePercent=1.25m, Description="For growing meal subscription outlets." };
        var pro = new SaaSPlan { Id=Guid.NewGuid(), Name="Professional", MonthlyFee=4999, AnnualFee=49990, IncludedActiveCustomers=500, AdditionalCustomerFee=8, CustomerTransactionFeePercent=.75m, Description="For established meal businesses." };
        db.SaaSPlans.AddRange(free,basic,growth,pro);

        var fitId=Guid.NewGuid(); var abcId=Guid.NewGuid(); var customerId=Guid.NewGuid(); var fitAdminId=Guid.NewGuid(); var superId=Guid.NewGuid();
        db.Outlets.AddRange(
            new Outlet { Id=fitId, Name="FitFood Bengaluru", Slug="fitfood", Subdomain="fitfood", City="Bengaluru", State="Karnataka", Pincode="560001", Latitude=12.9716, Longitude=77.5946, ServiceRadiusKm=20, Status=OutletStatus.Active, BillingPlan=BillingPlan.Growth },
            new Outlet { Id=abcId, Name="ABC Healthy Meals Mumbai", Slug="abc", Subdomain="abc", City="Mumbai", State="Maharashtra", Pincode="400001", Latitude=19.076, Longitude=72.8777, ServiceRadiusKm=20, Status=OutletStatus.Active, BillingPlan=BillingPlan.Starter, PrimaryColor="#166534" });
        db.Users.AddRange(
            new User { Id=customerId, Email="customer@healthapp.test", PasswordHash=passwords.Hash("demo"), FirstName="Demo", LastName="Customer", Role=UserRole.Customer },
            new User { Id=fitAdminId, Email="admin@fitfood.test", PasswordHash=passwords.Hash("demo"), FirstName="FitFood", LastName="Admin", Role=UserRole.OutletAdmin, OutletId=fitId },
            new User { Id=superId, Email="admin@healthapp.test", PasswordHash=passwords.Hash("demo"), FirstName="HealthApp", LastName="Admin", Role=UserRole.SuperAdmin });
        db.OutletSubscriptions.Add(new OutletSubscription { Id=Guid.NewGuid(), OutletId=fitId, SaaSPlanId=growth.Id, BillingCycle="Monthly", SubscriptionFee=growth.MonthlyFee, TransactionFeePercent=growth.CustomerTransactionFeePercent, StartDate=DateTime.UtcNow.Date, RenewalDate=DateTime.UtcNow.Date.AddMonths(1), Status="Active" });

        var p1 = new MealPlan { Id=Guid.NewGuid(), OutletId=fitId, Name="Healthy Weekly", Frequency="Weekly", MealsPerDay=2, MealsPerWeek=14, Price=2499, Currency="INR", Description="Balanced Indian meals for the week." };
        var p2 = new MealPlan { Id=Guid.NewGuid(), OutletId=fitId, Name="Performance Biweekly", Frequency="Biweekly", MealsPerDay=2, MealsPerWeek=14, Price=4599, Currency="INR", Description="Higher protein meal subscription." };
        var p3 = new MealPlan { Id=Guid.NewGuid(), OutletId=abcId, Name="Monthly Wellness", Frequency="Monthly", MealsPerDay=2, MealsPerWeek=14, Price=8499, Currency="INR", Description="Everyday wholesome meals." };
        db.MealPlans.AddRange(p1,p2,p3);

        var paneer=new Recipe{Id=Guid.NewGuid(),OutletId=fitId,Name="Paneer Power Bowl",Calories=520,ProteinGrams=32,CarbsGrams=48,FatGrams=18,Category=RecipeCategory.Veg,PricePerMeal=180,LargePricePerMeal=220,Description="Paneer, grains and seasonal vegetables."};
        var chicken=new Recipe{Id=Guid.NewGuid(),OutletId=fitId,Name="Chicken Tikka Bowl",Calories=560,ProteinGrams=42,CarbsGrams=52,FatGrams=16,Category=RecipeCategory.NonVeg,PricePerMeal=220,LargePricePerMeal=260,Description="Grilled chicken with rice and vegetables."};
        var chickpea=new Recipe{Id=Guid.NewGuid(),OutletId=fitId,Name="Chickpea Buddha Bowl",Calories=480,ProteinGrams=20,CarbsGrams=55,FatGrams=14,Category=RecipeCategory.Vegan,PricePerMeal=160,LargePricePerMeal=200,Description="Chickpeas, grains, greens and tahini."};
        var dal=new Recipe{Id=Guid.NewGuid(),OutletId=fitId,Name="Dal Khichdi",Calories=440,ProteinGrams=18,CarbsGrams=58,FatGrams=10,Category=RecipeCategory.Veg,PricePerMeal=140,LargePricePerMeal=180,Description="Comforting lentil and rice meal."};
        var prawn=new Recipe{Id=Guid.NewGuid(),OutletId=fitId,Name="Prawn Noodles",Calories=610,ProteinGrams=31,CarbsGrams=66,FatGrams=22,Category=RecipeCategory.NonVeg,PricePerMeal=240,LargePricePerMeal=280,Description="Wok-tossed prawns, vegetables and noodles.",Tags="High Protein,Seafood"};
        var mutton=new Recipe{Id=Guid.NewGuid(),OutletId=fitId,Name="Mutton Curry",Calories=650,ProteinGrams=38,CarbsGrams=48,FatGrams=29,Category=RecipeCategory.NonVeg,PricePerMeal=260,LargePricePerMeal=310,Description="Slow-cooked mutton curry with aromatic spices.",Tags="High Protein"};
        db.Recipes.AddRange(paneer,chicken,chickpea,dal,prawn,mutton);

        var ing=await db.Ingredients.ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        var alg=await db.Allergens.ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        AddRecipeIngredients(paneer,[("Paneer",120m,"g"),("Brown Rice",150m,"g"),("Broccoli",80m,"g"),("Olive Oil",10m,"g")],ing);
        AddRecipeIngredients(chicken,[("Chicken Breast",150m,"g"),("Brown Rice",150m,"g"),("Broccoli",80m,"g"),("Olive Oil",10m,"g")],ing);
        AddRecipeIngredients(chickpea,[("Chickpeas",140m,"g"),("Brown Rice",120m,"g"),("Broccoli",70m,"g"),("Tahini",20m,"g")],ing);
        AddRecipeIngredients(dal,[("Lentils",120m,"g"),("Brown Rice",160m,"g"),("Broccoli",60m,"g"),("Olive Oil",8m,"g")],ing);
        AddRecipeIngredients(prawn,[("Prawns",140m,"g"),("Wheat Noodles",150m,"g"),("Broccoli",80m,"g"),("Olive Oil",10m,"g")],ing);
        AddRecipeIngredients(mutton,[("Mutton",150m,"g"),("Brown Rice",150m,"g"),("Broccoli",70m,"g"),("Olive Oil",10m,"g")],ing);
        
        foreach(var day in new[]{DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday,DayOfWeek.Saturday})
        {
            db.OutletMenuItems.AddRange(
                new OutletMenuItem{Id=Guid.NewGuid(),OutletId=fitId,RecipeId=paneer.Id,DayOfWeek=day,MealSlot=MealSlot.Afternoon,DisplayOrder=1},
                new OutletMenuItem{Id=Guid.NewGuid(),OutletId=fitId,RecipeId=chicken.Id,DayOfWeek=day,MealSlot=MealSlot.Afternoon,DisplayOrder=2},
                new OutletMenuItem{Id=Guid.NewGuid(),OutletId=fitId,RecipeId=chickpea.Id,DayOfWeek=day,MealSlot=MealSlot.Afternoon,DisplayOrder=3},
                new OutletMenuItem{Id=Guid.NewGuid(),OutletId=fitId,RecipeId=chicken.Id,DayOfWeek=day,MealSlot=MealSlot.Evening,DisplayOrder=1},
                new OutletMenuItem{Id=Guid.NewGuid(),OutletId=fitId,RecipeId=prawn.Id,DayOfWeek=day,MealSlot=MealSlot.Evening,DisplayOrder=2});
        }
        db.OutletMenuItems.Add(new OutletMenuItem{Id=Guid.NewGuid(),OutletId=fitId,RecipeId=mutton.Id,DayOfWeek=DayOfWeek.Tuesday,MealSlot=MealSlot.Afternoon,DisplayOrder=4});
        
        var areas = new[]
        {
            new CityArea{Id=Guid.NewGuid(),City="Bengaluru",State="Karnataka",Name="Indiranagar",Pincode="560038",Latitude=12.9784,Longitude=77.6408},
            new CityArea{Id=Guid.NewGuid(),City="Bengaluru",State="Karnataka",Name="Koramangala",Pincode="560034",Latitude=12.9352,Longitude=77.6245},
            new CityArea{Id=Guid.NewGuid(),City="Bengaluru",State="Karnataka",Name="HSR Layout",Pincode="560102",Latitude=12.9116,Longitude=77.6389},
            new CityArea{Id=Guid.NewGuid(),City="Bengaluru",State="Karnataka",Name="Whitefield",Pincode="560066",Latitude=12.9698,Longitude=77.7499},
            new CityArea{Id=Guid.NewGuid(),City="Bengaluru",State="Karnataka",Name="Jayanagar",Pincode="560041",Latitude=12.9250,Longitude=77.5938},
            new CityArea{Id=Guid.NewGuid(),City="Bengaluru",State="Karnataka",Name="Malleshwaram",Pincode="560003",Latitude=13.0035,Longitude=77.5700},
            new CityArea{Id=Guid.NewGuid(),City="Mumbai",State="Maharashtra",Name="Bandra",Pincode="400050",Latitude=19.0607,Longitude=72.8362},
            new CityArea{Id=Guid.NewGuid(),City="Mumbai",State="Maharashtra",Name="Andheri",Pincode="400053",Latitude=19.1197,Longitude=72.8468}
        };
        db.CityAreas.AddRange(areas);
        var fitAreas=areas.Where(x=>x.City=="Bengaluru").Take(5).Select(x=>new OutletDeliveryArea{Id=Guid.NewGuid(),OutletId=fitId,CityAreaId=x.Id}).ToList();
        db.OutletDeliveryAreas.AddRange(fitAreas);
        db.DeliveryPricingRules.AddRange(
            new DeliveryPricingRule{Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=2,Fee=10},
            new DeliveryPricingRule{Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=5,Fee=20},
            new DeliveryPricingRule{Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=10,Fee=30},
            new DeliveryPricingRule{Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=15,Fee=50},
            new DeliveryPricingRule{Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=20,Fee=70});
        db.SubscriptionDiscountTiers.AddRange(
            new SubscriptionDiscountTier{Id=Guid.NewGuid(),OutletId=fitId,MinMeals=1,MaxMeals=9,OneWeekPercent=0,TwoWeeksPercent=0,OneMonthPercent=0},
            new SubscriptionDiscountTier{Id=Guid.NewGuid(),OutletId=fitId,MinMeals=10,MaxMeals=19,OneWeekPercent=3,TwoWeeksPercent=3,OneMonthPercent=2},
            new SubscriptionDiscountTier{Id=Guid.NewGuid(),OutletId=fitId,MinMeals=20,MaxMeals=29,OneWeekPercent=4,TwoWeeksPercent=5,OneMonthPercent=3},
            new SubscriptionDiscountTier{Id=Guid.NewGuid(),OutletId=fitId,MinMeals=30,MaxMeals=49,OneWeekPercent=5,TwoWeeksPercent=6,OneMonthPercent=5},
            new SubscriptionDiscountTier{Id=Guid.NewGuid(),OutletId=fitId,MinMeals=50,MaxMeals=null,OneWeekPercent=6,TwoWeeksPercent=7,OneMonthPercent=6});
        db.CustomerAddresses.Add(new CustomerAddress{Id=Guid.NewGuid(),CustomerId=customerId,CityAreaId=areas.First(x=>x.Name=="Indiranagar").Id,Label="Home",AddressLine1="100 12th Main Road",AddressLine2="Indiranagar",ContactName="Demo Customer",ContactPhone="9999999999",Latitude=12.9784,Longitude=77.6408,IsDefault=true});

        var demoCustomer=await db.Users.FirstAsync(x=>x.Email=="customer@healthapp.test",ct);
        foreach(var name in new[]{"Milk","Shellfish"})
        {
            var a=alg[name];
            if(!await db.CustomerAllergies.AnyAsync(x=>x.CustomerId==demoCustomer.Id&&x.AllergenId==a.Id,ct))
                db.CustomerAllergies.Add(new CustomerAllergy{Id=Guid.NewGuid(),CustomerId=demoCustomer.Id,AllergenId=a.Id});
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCatalogAsync(HealthAppDbContext db,CancellationToken ct)
    {
        var allergenNames=new[]{"Milk","Egg","Peanuts","Tree Nuts","Soy","Wheat/Gluten","Sesame","Fish","Shellfish"};
        var allergens=await db.Allergens.ToListAsync(ct);
        foreach(var name in allergenNames)
            if(!allergens.Any(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))
                db.Allergens.Add(new Allergen{Id=Guid.NewGuid(),Name=name});
        var ingredientNames=new[]{
            ("Chicken Breast","g"),("Olive Oil","g"),("Paneer","g"),("Brown Rice","g"),("Broccoli","g"),
            ("Chickpeas","g"),("Tahini","g"),("Lentils","g"),("Prawns","g"),("Wheat Noodles","g"),("Mutton","g")
        };
        var ingredients=await db.Ingredients.ToListAsync(ct);
        foreach(var item in ingredientNames)
            if(!ingredients.Any(x=>x.Name.Equals(item.Item1,StringComparison.OrdinalIgnoreCase)))
                db.Ingredients.Add(new Ingredient{Id=Guid.NewGuid(),Name=item.Item1,DefaultUnit=item.Item2});
        await db.SaveChangesAsync(ct);

        var all=await db.Allergens.ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        var ing=await db.Ingredients.ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        LinkIngredientAllergen(db,ing["Paneer"],all["Milk"]);
        LinkIngredientAllergen(db,ing["Tahini"],all["Sesame"]);
        LinkIngredientAllergen(db,ing["Prawns"],all["Shellfish"]);
        LinkIngredientAllergen(db,ing["Wheat Noodles"],all["Wheat/Gluten"]);
        await db.SaveChangesAsync(ct);
    }

    private static void LinkIngredientAllergen(HealthAppDbContext db,Ingredient ingredient,Allergen allergen)
    {
        if(!db.IngredientAllergens.Any(x=>x.IngredientId==ingredient.Id&&x.AllergenId==allergen.Id))
            db.IngredientAllergens.Add(new IngredientAllergen{IngredientId=ingredient.Id,AllergenId=allergen.Id});
    }

    private static void AddRecipeIngredients(Recipe recipe,(string Name,decimal Quantity,string Unit)[] items,Dictionary<string,Ingredient> ingredients)
    {
        foreach(var item in items)
        {
            var ingredient=ingredients[item.Name];
            recipe.RecipeIngredients.Add(new RecipeIngredient{Id=Guid.NewGuid(),RecipeId=recipe.Id,IngredientId=ingredient.Id,Quantity=item.Quantity,Unit=item.Unit});
        }
    }

    private static async Task EnsureExistingRecipeCatalogLinksAsync(HealthAppDbContext db,CancellationToken ct)
    {
        var recipes=await db.Recipes.ToListAsync(ct);
        var ingredients=await db.Ingredients.ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        var wanted=new Dictionary<string,(string Name,decimal Quantity,string Unit)[]>
        {
            ["Paneer Power Bowl"]=[("Paneer",120,"g"),("Brown Rice",150,"g"),("Broccoli",80,"g"),("Olive Oil",10,"g")],
            ["Chicken Tikka Bowl"]=[("Chicken Breast",150,"g"),("Brown Rice",150,"g"),("Broccoli",80,"g"),("Olive Oil",10,"g")],
            ["Chickpea Buddha Bowl"]=[("Chickpeas",140,"g"),("Brown Rice",120,"g"),("Broccoli",70,"g"),("Tahini",20,"g")],
            ["Dal Khichdi"]=[("Lentils",120,"g"),("Brown Rice",160,"g"),("Broccoli",60,"g"),("Olive Oil",8,"g")],
            ["Prawn Noodles"]=[("Prawns",140,"g"),("Wheat Noodles",150,"g"),("Broccoli",80,"g"),("Olive Oil",10,"g")],
            ["Mutton Curry"]=[("Mutton",150,"g"),("Brown Rice",150,"g"),("Broccoli",70,"g"),("Olive Oil",10,"g")]
        };
        foreach(var recipe in recipes)
        {
            if(!wanted.TryGetValue(recipe.Name,out var items)) continue;
            var existing=await db.RecipeIngredients.Where(x=>x.RecipeId==recipe.Id).ToListAsync(ct);
            if(existing.Count==0) AddRecipeIngredients(recipe,items,ingredients);
        }
        var customer=await db.Users.FirstOrDefaultAsync(x=>x.Email=="customer@healthapp.test",ct);
        if(customer is not null)
        {
            var all=await db.Allergens.ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
            foreach(var name in new[]{"Milk","Shellfish"})
                if(all.TryGetValue(name,out var a)&&!await db.CustomerAllergies.AnyAsync(x=>x.CustomerId==customer.Id&&x.AllergenId==a.Id,ct))
                    db.CustomerAllergies.Add(new CustomerAllergy{Id=Guid.NewGuid(),CustomerId=customer.Id,AllergenId=a.Id});
        }
        await db.SaveChangesAsync(ct);
    }
}
