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
        await EnsureServiceCitiesAsync(db, ct);
        if (await db.Outlets.AnyAsync(ct))
        {
            await EnsureExistingRecipeCatalogLinksAsync(db, ct);
            await EnsureExistingOutletMenuSlotsAsync(db, ct);
            await EnsureRegionalOutletCatalogAsync(db, passwords, ct);
            return;
        }
        var free = new SaaSPlan {
            Id=Guid.NewGuid(),
            Name="Free",
            MonthlyFee=0,
            AnnualFee=0,
            IncludedActiveCustomers=10,
            AdditionalCustomerFee=0,
            CustomerTransactionFeePercent=3m,
            Description="Free plan for up to 10 active customers."
        };
        var basic = new SaaSPlan {
            Id=Guid.NewGuid(),
            Name="Basic",
            MonthlyFee=999,
            AnnualFee=9990,
            IncludedActiveCustomers=50,
            AdditionalCustomerFee=15,
            CustomerTransactionFeePercent=2m,
            Description="For small meal businesses."
        };
        var growth = new SaaSPlan {
            Id=Guid.NewGuid(),
            Name="Growth",
            MonthlyFee=2499,
            AnnualFee=24990,
            IncludedActiveCustomers=150,
            AdditionalCustomerFee=12,
            CustomerTransactionFeePercent=1.25m,
            Description="For growing meal subscription outlets."
        };
        var pro = new SaaSPlan {
            Id=Guid.NewGuid(),
            Name="Professional",
            MonthlyFee=4999,
            AnnualFee=49990,
            IncludedActiveCustomers=500,
            AdditionalCustomerFee=8,
            CustomerTransactionFeePercent=.75m,
            Description="For established meal businesses."
        };
        db.SaaSPlans.AddRange(free,basic,growth,pro);
        var fitId=Guid.NewGuid();
        var abcId=Guid.NewGuid();
        var customerId=Guid.NewGuid();
        var fitAdminId=Guid.NewGuid();
        var driverId=Guid.NewGuid();
        var superId=Guid.NewGuid();
        db.Outlets.AddRange(
        new Outlet {
            Id=fitId, Name="FitFood Bengaluru", Slug="fitfood", Subdomain="fitfood", City="Bengaluru", State="Karnataka", Pincode="560001", Latitude=12.9716, Longitude=77.5946, ServiceRadiusKm=20, Status=OutletStatus.Live, DeliveryDays="Monday,Tuesday,Wednesday,Thursday,Friday,Saturday", BillingPlan=BillingPlan.Growth, LogoUrl="https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=160&q=80", HeroImageUrl="https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=1200&q=85", HealthHighlights="Grilled,Cold Pressed Oil,High Protein,Exotic Bowls,Fresh Ingredients"
        },
        new Outlet {
            Id=abcId, Name="ABC Healthy Meals Mumbai", Slug="abc", Subdomain="abc", City="Mumbai", State="Maharashtra", Pincode="400001", Latitude=19.076, Longitude=72.8777, ServiceRadiusKm=20, Status=OutletStatus.Live, DeliveryDays="Monday,Tuesday,Wednesday,Thursday,Friday,Saturday", BillingPlan=BillingPlan.Starter, PrimaryColor="#166534", LogoUrl="https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=160&q=80", HeroImageUrl="https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=1200&q=85", HealthHighlights="Fresh Ingredients,Balanced Nutrition,Vegetarian Friendly,High Protein"
        });
        db.Users.AddRange(
        new User {
            Id=customerId, Email="customer@healthapp.test", PasswordHash=passwords.Hash("demo"), FirstName="Demo", LastName="Customer", Role=UserRole.Customer, OutletId=fitId
        },
        new User {
            Id=fitAdminId, Email="admin@fitfood.test", PasswordHash=passwords.Hash("demo"), FirstName="FitFood", LastName="Admin", Role=UserRole.OutletAdmin, OutletId=fitId
        },
        new User {
            Id=driverId, Email="driver@fitfood.test", PasswordHash=passwords.Hash("demo"), FirstName="FitFood", LastName="Driver", Role=UserRole.Driver, OutletId=fitId
        },
        new User {
            Id=superId, Email="admin@healthapp.test", PasswordHash=passwords.Hash("demo"), FirstName="HealthApp", LastName="Admin", Role=UserRole.SuperAdmin
        });
        db.OutletSubscriptions.Add(new OutletSubscription {
            Id=Guid.NewGuid(), OutletId=fitId, SaaSPlanId=growth.Id, BillingCycle="Monthly", SubscriptionFee=growth.MonthlyFee, TransactionFeePercent=growth.CustomerTransactionFeePercent, StartDate=DateTime.UtcNow.Date, RenewalDate=DateTime.UtcNow.Date.AddMonths(1), Status="Active"
        });
        var p1 = new MealPlan {
            Id=Guid.NewGuid(),
            OutletId=fitId,
            Name="Healthy Weekly",
            Frequency="Weekly",
            MealsPerDay=2,
            MealsPerWeek=14,
            Price=2499,
            Currency="INR",
            Description="Balanced Indian meals for the week."
        };
        var p2 = new MealPlan {
            Id=Guid.NewGuid(),
            OutletId=fitId,
            Name="Performance Biweekly",
            Frequency="Biweekly",
            MealsPerDay=2,
            MealsPerWeek=14,
            Price=4599,
            Currency="INR",
            Description="Higher protein meal subscription."
        };
        var p3 = new MealPlan {
            Id=Guid.NewGuid(),
            OutletId=abcId,
            Name="Monthly Wellness",
            Frequency="Monthly",
            MealsPerDay=2,
            MealsPerWeek=14,
            Price=8499,
            Currency="INR",
            Description="Everyday wholesome meals."
        };
        db.MealPlans.AddRange(p1,p2,p3);
        var paneer=new Recipe {
            Id=Guid.NewGuid(),
            OutletId=fitId,
            Name="Paneer Power Bowl",
            Calories=520,
            ProteinGrams=32,
            CarbsGrams=48,
            FatGrams=18,
            Category=RecipeCategory.Veg,
            PricePerMeal=180,
            LargePricePerMeal=220,
            Description="Paneer, grains and seasonal vegetables."
        };
        var chicken=new Recipe {
            Id=Guid.NewGuid(),
            OutletId=fitId,
            Name="Chicken Tikka Bowl",
            Calories=560,
            ProteinGrams=42,
            CarbsGrams=52,
            FatGrams=16,
            Category=RecipeCategory.NonVeg,
            PricePerMeal=220,
            LargePricePerMeal=260,
            Description="Grilled chicken with rice and vegetables."
        };
        var chickpea=new Recipe {
            Id=Guid.NewGuid(),
            OutletId=fitId,
            Name="Chickpea Buddha Bowl",
            Calories=480,
            ProteinGrams=20,
            CarbsGrams=55,
            FatGrams=14,
            Category=RecipeCategory.Vegan,
            PricePerMeal=160,
            LargePricePerMeal=200,
            Description="Chickpeas, grains, greens and tahini."
        };
        var dal=new Recipe {
            Id=Guid.NewGuid(),
            OutletId=fitId,
            Name="Dal Khichdi",
            Calories=440,
            ProteinGrams=18,
            CarbsGrams=58,
            FatGrams=10,
            Category=RecipeCategory.Veg,
            PricePerMeal=140,
            LargePricePerMeal=180,
            Description="Comforting lentil and rice meal."
        };
        var prawn=new Recipe {
            Id=Guid.NewGuid(),
            OutletId=fitId,
            Name="Prawn Noodles",
            Calories=610,
            ProteinGrams=31,
            CarbsGrams=66,
            FatGrams=22,
            Category=RecipeCategory.NonVeg,
            PricePerMeal=240,
            LargePricePerMeal=280,
            Description="Wok-tossed prawns, vegetables and noodles.",
            Tags="High Protein,Seafood"
        };
        var mutton=new Recipe {
            Id=Guid.NewGuid(),
            OutletId=fitId,
            Name="Mutton Curry",
            Calories=650,
            ProteinGrams=38,
            CarbsGrams=48,
            FatGrams=29,
            Category=RecipeCategory.NonVeg,
            PricePerMeal=260,
            LargePricePerMeal=310,
            Description="Slow-cooked mutton curry with aromatic spices.",
            Tags="High Protein"
        };
        db.Recipes.AddRange(paneer,chicken,chickpea,dal,prawn,mutton);
        var ing=await db.Ingredients
            .GroupBy(x=>x.Name)
            .Select(g=>g.First())
            .ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        var alg=await db.Allergens.ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        AddRecipeIngredients(paneer,[("Paneer",120m,"g"),("Brown Rice",150m,"g"),("Broccoli",80m,"g"),("Olive Oil",10m,"g")],ing);
        AddRecipeIngredients(chicken,[("Chicken Breast",150m,"g"),("Brown Rice",150m,"g"),("Broccoli",80m,"g"),("Olive Oil",10m,"g")],ing);
        AddRecipeIngredients(chickpea,[("Chickpeas",140m,"g"),("Brown Rice",120m,"g"),("Broccoli",70m,"g"),("Tahini",20m,"g")],ing);
        AddRecipeIngredients(dal,[("Lentils",120m,"g"),("Brown Rice",160m,"g"),("Broccoli",60m,"g"),("Olive Oil",8m,"g")],ing);
        AddRecipeIngredients(prawn,[("Prawns",140m,"g"),("Wheat Noodles",150m,"g"),("Broccoli",80m,"g"),("Olive Oil",10m,"g")],ing);
        AddRecipeIngredients(mutton,[("Mutton",150m,"g"),("Brown Rice",150m,"g"),("Broccoli",70m,"g"),("Olive Oil",10m,"g")],ing);
        foreach(var day in new[] {
            DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday,DayOfWeek.Saturday
        })
        {
            db.OutletMenuItems.AddRange(
            new OutletMenuItem {
                Id=Guid.NewGuid(),OutletId=fitId,RecipeId=paneer.Id,DayOfWeek=day,MealSlot=MealSlot.Afternoon,DisplayOrder=1
            },
            new OutletMenuItem {
                Id=Guid.NewGuid(),OutletId=fitId,RecipeId=chicken.Id,DayOfWeek=day,MealSlot=MealSlot.Afternoon,DisplayOrder=2
            },
            new OutletMenuItem {
                Id=Guid.NewGuid(),OutletId=fitId,RecipeId=chickpea.Id,DayOfWeek=day,MealSlot=MealSlot.Afternoon,DisplayOrder=3
            },
            new OutletMenuItem {
                Id=Guid.NewGuid(),OutletId=fitId,RecipeId=chicken.Id,DayOfWeek=day,MealSlot=MealSlot.Evening,DisplayOrder=1
            },
            new OutletMenuItem {
                Id=Guid.NewGuid(),OutletId=fitId,RecipeId=prawn.Id,DayOfWeek=day,MealSlot=MealSlot.Evening,DisplayOrder=2
            });
        }
        db.OutletMenuItems.Add(new OutletMenuItem {
            Id=Guid.NewGuid(),OutletId=fitId,RecipeId=mutton.Id,DayOfWeek=DayOfWeek.Tuesday,MealSlot=MealSlot.Afternoon,DisplayOrder=4
        });
        var areas = new[]
        {
            new CityArea {
                Id=Guid.NewGuid(),
                City="Bengaluru",
                State="Karnataka",
                Name="Indiranagar",
                Pincode="560038",
                Latitude=12.9784,
                Longitude=77.6408
            },
            new CityArea {
                Id=Guid.NewGuid(),
                City="Bengaluru",
                State="Karnataka",
                Name="Koramangala",
                Pincode="560034",
                Latitude=12.9352,
                Longitude=77.6245
            },
            new CityArea {
                Id=Guid.NewGuid(),
                City="Bengaluru",
                State="Karnataka",
                Name="HSR Layout",
                Pincode="560102",
                Latitude=12.9116,
                Longitude=77.6389
            },
            new CityArea {
                Id=Guid.NewGuid(),
                City="Bengaluru",
                State="Karnataka",
                Name="Whitefield",
                Pincode="560066",
                Latitude=12.9698,
                Longitude=77.7499
            },
            new CityArea {
                Id=Guid.NewGuid(),
                City="Bengaluru",
                State="Karnataka",
                Name="Jayanagar",
                Pincode="560041",
                Latitude=12.9250,
                Longitude=77.5938
            },
            new CityArea {
                Id=Guid.NewGuid(),
                City="Bengaluru",
                State="Karnataka",
                Name="Malleshwaram",
                Pincode="560003",
                Latitude=13.0035,
                Longitude=77.5700
            },
            new CityArea {
                Id=Guid.NewGuid(),
                City="Mumbai",
                State="Maharashtra",
                Name="Bandra",
                Pincode="400050",
                Latitude=19.0607,
                Longitude=72.8362
            },
            new CityArea {
                Id=Guid.NewGuid(),
                City="Mumbai",
                State="Maharashtra",
                Name="Andheri",
                Pincode="400053",
                Latitude=19.1197,
                Longitude=72.8468
            }
        };
        db.CityAreas.AddRange(areas);
        var fitAreas=areas.Where(x=>x.City=="Bengaluru").Take(5).Select(x=>new OutletDeliveryArea {
            Id=Guid.NewGuid(),OutletId=fitId,CityAreaId=x.Id
        }).ToList();
        db.OutletDeliveryAreas.AddRange(fitAreas);
        db.DeliveryPricingRules.AddRange(
        new DeliveryPricingRule {
            Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=2,Fee=10
        },
        new DeliveryPricingRule {
            Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=5,Fee=20
        },
        new DeliveryPricingRule {
            Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=10,Fee=30
        },
        new DeliveryPricingRule {
            Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=15,Fee=50
        },
        new DeliveryPricingRule {
            Id=Guid.NewGuid(),OutletId=fitId,MaxDistanceKm=20,Fee=70
        });
        db.SubscriptionDiscountTiers.AddRange(
        new SubscriptionDiscountTier {
            Id=Guid.NewGuid(),OutletId=fitId,MinMeals=1,MaxMeals=9,OneWeekPercent=0,TwoWeeksPercent=0,OneMonthPercent=0
        },
        new SubscriptionDiscountTier {
            Id=Guid.NewGuid(),OutletId=fitId,MinMeals=10,MaxMeals=19,OneWeekPercent=3,TwoWeeksPercent=3,OneMonthPercent=2
        },
        new SubscriptionDiscountTier {
            Id=Guid.NewGuid(),OutletId=fitId,MinMeals=20,MaxMeals=29,OneWeekPercent=4,TwoWeeksPercent=5,OneMonthPercent=3
        },
        new SubscriptionDiscountTier {
            Id=Guid.NewGuid(),OutletId=fitId,MinMeals=30,MaxMeals=49,OneWeekPercent=5,TwoWeeksPercent=6,OneMonthPercent=5
        },
        new SubscriptionDiscountTier {
            Id=Guid.NewGuid(),OutletId=fitId,MinMeals=50,MaxMeals=null,OneWeekPercent=6,TwoWeeksPercent=7,OneMonthPercent=6
        });
        db.CustomerAddresses.Add(new CustomerAddress {
            Id=Guid.NewGuid(),CustomerId=customerId,CityAreaId=areas.First(x=>x.Name=="Indiranagar").Id,City="Bengaluru",State="Karnataka",Pincode="560038",Locality="Indiranagar",Label="Home",AddressLine1="100 12th Main Road",AddressLine2="Indiranagar",ContactName="Demo Customer",ContactPhone="9999999999",Latitude=12.9784,Longitude=77.6408,IsDefault=true
        });
        var demoCustomerId = customerId;
        foreach(var name in new[] {
            "Milk","Shellfish"
        })
        {
            var a=alg[name];
            if(!await db.CustomerAllergies.AnyAsync(x=>x.CustomerId==demoCustomerId&&x.AllergenId==a.Id,ct))
            db.CustomerAllergies.Add(new CustomerAllergy {
                Id=Guid.NewGuid(),CustomerId=demoCustomerId,AllergenId=a.Id
            });
        }
        await db.SaveChangesAsync(ct);
        await EnsureRegionalOutletCatalogAsync(db, passwords, ct);
    }

    private static async Task EnsureServiceCitiesAsync(HealthAppDbContext db, CancellationToken ct)
    {
        var specs = new[]
        {
            ("Bengaluru", "Karnataka", 12.9716, 77.5946),
            ("Mumbai", "Maharashtra", 19.0760, 72.8777),
            ("Chennai", "Tamil Nadu", 13.0827, 80.2707),
            ("New Delhi", "Delhi", 28.6139, 77.2090),
            ("Hyderabad", "Telangana", 17.3850, 78.4867)
        };

        foreach (var spec in specs)
        {
            var city = await db.ServiceCities.FirstOrDefaultAsync(x => x.City == spec.Item1, ct);
            if (city is null)
            {
                db.ServiceCities.Add(new ServiceCity
                {
                    Id = Guid.NewGuid(),
                    City = spec.Item1,
                    State = spec.Item2,
                    Country = "India",
                    Latitude = spec.Item3,
                    Longitude = spec.Item4,
                    IsEnabled = true
                });
            }
            else
            {
                city.State = spec.Item2;
                city.Country = "India";
                city.Latitude = spec.Item3;
                city.Longitude = spec.Item4;
                // Preserve the Super Admin's enable/disable choice on existing cities.
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureRegionalOutletCatalogAsync(HealthAppDbContext db, IPasswordService passwords, CancellationToken ct)
    {
        var basicPlan = await db.SaaSPlans.FirstOrDefaultAsync(x => x.Name == "Basic", ct);
        if (basicPlan is null)
        {
            basicPlan = new SaaSPlan
            {
                Id = Guid.NewGuid(),
                Name = "Basic",
                MonthlyFee = 999,
                AnnualFee = 9990,
                IncludedActiveCustomers = 50,
                AdditionalCustomerFee = 15,
                CustomerTransactionFeePercent = 2m,
                Description = "For small meal businesses."
            };
            db.SaaSPlans.Add(basicPlan);
            await db.SaveChangesAsync(ct);
        }

        var citySpecs = new[]
        {
            new
            {
                Slug = "andhra-ruchulu",
                Name = "Andhra Ruchulu Chennai",
                Subdomain = "andhraruchulu",
                City = "Chennai",
                State = "Tamil Nadu",
                Pincode = "600001",
                Latitude = 13.0827,
                Longitude = 80.2707,
                Color = "#b91c1c",
                Hero = "https://media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_300%2Ch_300%2Cc_fit/FOOD_CATALOG/IMAGES/CMS/2024/5/20/736b159e-4973-43ed-bd14-fdece423f486_b24651c4-c34c-4801-a94a-cd8b95261ca4.jpg",
                Highlights = "Andhra Meals,Spicy Curries,Banana Leaf Meals,South Indian Classics,Freshly Cooked",
                Rating = 4.8, ReviewCount = 320,
                About = "Authentic Andhra and South Indian meals prepared fresh with regional spices and balanced portions.",
                Areas = new[] { ("T Nagar", "600017", 13.0418, 80.2341), ("Adyar", "600020", 13.0063, 80.2574), ("Velachery", "600042", 12.9755, 80.2211), ("Anna Nagar", "600040", 13.0850, 80.2101) }
            },
            new
            {
                Slug = "hyderabad-zaika",
                Name = "Hyderabad Zaika",
                Subdomain = "hyderabadzaika",
                City = "Hyderabad",
                State = "Telangana",
                Pincode = "500001",
                Latitude = 17.3850,
                Longitude = 78.4867,
                Color = "#9a3412",
                Hero = "https://images.unsplash.com/photo-1563379091339-03246963d29a?auto=format&fit=crop&w=1200&q=85",
                Highlights = "Hyderabadi Biryani,Haleem,Charcoal Grilled,Slow Cooked,Authentic Spices",
                Rating = 4.9, ReviewCount = 485,
                About = "Specialist in Hyderabadi biryani, slow-cooked haleem and traditional Deccan flavours.",
                Areas = new[] { ("Banjara Hills", "500034", 17.4156, 78.4347), ("Jubilee Hills", "500033", 17.4319, 78.4071), ("Hitech City", "500081", 17.4435, 78.3772), ("Secunderabad", "500003", 17.4399, 78.4983) }
            },
            new
            {
                Slug = "deccan-wok",
                Name = "Deccan Wok",
                Subdomain = "deccanwok",
                City = "Hyderabad",
                State = "Telangana",
                Pincode = "500016",
                Latitude = 17.4442,
                Longitude = 78.4483,
                Color = "#0369a1",
                Hero = "https://images.unsplash.com/photo-1603133872878-684f208fb84b?auto=format&fit=crop&w=1200&q=85",
                Highlights = "Indo-Chinese,Hakka Noodles,Fried Rice,Wok Tossed,Fast & Fresh",
                Rating = 4.7, ReviewCount = 265,
                About = "Fast, fresh Indo-Chinese dishes wok-tossed to order with bold sauces and crisp vegetables.",
                Areas = new[] { ("Begumpet", "500016", 17.4442, 78.4483), ("Ameerpet", "500016", 17.4375, 78.4483), ("Kukatpally", "500072", 17.4948, 78.3996), ("Madhapur", "500081", 17.4483, 78.3915) }
            },
            new
            {
                Slug = "dilli-rasoi",
                Name = "Dilli Rasoi Delhi",
                Subdomain = "dillirasoi",
                City = "New Delhi",
                State = "Delhi",
                Pincode = "110001",
                Latitude = 28.6139,
                Longitude = 77.2090,
                Color = "#b45309",
                Hero = "https://masalapolska.com/assets/indian_thali_meal_top_down-BK7jUhaG.png",
                Highlights = "North Indian Classics,Tandoor Specials,Rich Curries,Homestyle Thalis,Basmati Rice",
                Rating = 4.6, ReviewCount = 210,
                About = "North Indian comfort food with homestyle thalis, tandoor favourites and rich regional curries.",
                Areas = new[] { ("Connaught Place", "110001", 28.6315, 77.2167), ("Karol Bagh", "110005", 28.6519, 77.1909), ("Saket", "110017", 28.5244, 77.2066), ("Dwarka", "110075", 28.5921, 77.0460) }
            }
        };

        foreach (var spec in citySpecs)
        {
            var outlet = await db.Outlets.FirstOrDefaultAsync(x => x.Slug == spec.Slug, ct);
            if (outlet is null)
            {
                outlet = new Outlet
                {
                    Id = Guid.NewGuid(),
                    Name = spec.Name,
                    Slug = spec.Slug,
                    Subdomain = spec.Subdomain,
                    City = spec.City,
                    State = spec.State,
                    Pincode = spec.Pincode,
                    Latitude = spec.Latitude,
                    Longitude = spec.Longitude,
                    ServiceRadiusKm = 15,
                    Status = OutletStatus.Live,
                    DeliveryDays = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday",
                    BillingPlan = BillingPlan.Starter,
                    LogoUrl = spec.Hero,
                    HeroImageUrl = spec.Hero,
                    HealthHighlights = spec.Highlights,
                    Rating = spec.Rating,
                    ReviewCount = spec.ReviewCount,
                    About = spec.About,
                    PrimaryColor = spec.Color,
                    PreparationCutoffHours = 24,
                    AllowMealSkipping = true,
                    CreditDeliveryFeeOnSkip = true
                };
                db.Outlets.Add(outlet);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                outlet.Name = spec.Name;
                outlet.City = spec.City;
                outlet.State = spec.State;
                outlet.Pincode = spec.Pincode;
                outlet.Latitude = spec.Latitude;
                outlet.Longitude = spec.Longitude;
                outlet.ServiceRadiusKm = 15;
                outlet.Status = OutletStatus.Live;
                outlet.DeliveryDays = string.IsNullOrWhiteSpace(outlet.DeliveryDays) ? "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday" : outlet.DeliveryDays;
                outlet.HeroImageUrl = spec.Hero;
                outlet.HealthHighlights = spec.Highlights;
                outlet.Rating = spec.Rating;
                outlet.ReviewCount = spec.ReviewCount;
                outlet.About = spec.About;
                outlet.PrimaryColor = spec.Color;
            }

            var adminEmail = $"{spec.Slug}.admin@healthapp.test";
            if (!await db.Users.AnyAsync(x => x.Email == adminEmail, ct))
            {
                db.Users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    Email = adminEmail,
                    PasswordHash = passwords.Hash("demo"),
                    FirstName = spec.City == "Chennai" ? "Andhra Ruchulu" : "Dilli Rasoi",
                    LastName = "Admin",
                    Role = UserRole.OutletAdmin,
                    OutletId = outlet.Id
                });
            }

            var driverEmail = $"{spec.Slug}.driver@healthapp.test";
            if (!await db.Users.AnyAsync(x => x.Email == driverEmail, ct))
            {
                db.Users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    Email = driverEmail,
                    PasswordHash = passwords.Hash("demo"),
                    FirstName = spec.City == "Chennai" ? "Andhra Ruchulu" : "Dilli Rasoi",
                    LastName = "Driver",
                    Role = UserRole.Driver,
                    OutletId = outlet.Id
                });
            }

            if (!await db.OutletSubscriptions.AnyAsync(x => x.OutletId == outlet.Id, ct))
            {
                db.OutletSubscriptions.Add(new OutletSubscription
                {
                    Id = Guid.NewGuid(),
                    OutletId = outlet.Id,
                    SaaSPlanId = basicPlan.Id,
                    BillingCycle = "Monthly",
                    SubscriptionFee = basicPlan.MonthlyFee,
                    TransactionFeePercent = basicPlan.CustomerTransactionFeePercent,
                    StartDate = DateTime.UtcNow.Date,
                    RenewalDate = DateTime.UtcNow.Date.AddMonths(1),
                    Status = "Active"
                });
            }

            foreach (var areaSpec in spec.Areas)
            {
                var area = await db.CityAreas.FirstOrDefaultAsync(
                    x => x.City == spec.City && x.Name == areaSpec.Item1, ct);

                if (area is null)
                {
                    area = new CityArea
                    {
                        Id = Guid.NewGuid(),
                        City = spec.City,
                        State = spec.State,
                        Name = areaSpec.Item1,
                        Pincode = areaSpec.Item2,
                        Latitude = areaSpec.Item3,
                        Longitude = areaSpec.Item4,
                        IsActive = true
                    };
                    db.CityAreas.Add(area);
                    await db.SaveChangesAsync(ct);
                }

                if (!await db.OutletDeliveryAreas.AnyAsync(
                    x => x.OutletId == outlet.Id && x.CityAreaId == area.Id, ct))
                {
                    db.OutletDeliveryAreas.Add(new OutletDeliveryArea
                    {
                        Id = Guid.NewGuid(),
                        OutletId = outlet.Id,
                        CityAreaId = area.Id,
                        IsActive = true
                    });
                }
            }

            foreach (var rule in new[] { (2m, 10m), (5m, 20m), (10m, 30m), (15m, 50m) })
            {
                if (!await db.DeliveryPricingRules.AnyAsync(
                    x => x.OutletId == outlet.Id && x.MaxDistanceKm == rule.Item1, ct))
                {
                    db.DeliveryPricingRules.Add(new DeliveryPricingRule
                    {
                        Id = Guid.NewGuid(),
                        OutletId = outlet.Id,
                        MaxDistanceKm = rule.Item1,
                        Fee = rule.Item2,
                        IsActive = true
                    });
                }
            }

            var planSpecs = new[]
            {
                ("Weekly Regional Favourites", "Weekly", 2, 14, 2499m, "Seven days of regional Indian comfort food with flexible meal selection."),
                ("Monthly Family Table", "Monthly", 2, 14, 8499m, "A four-week regional menu with lunch and dinner choices.")
            };

            foreach (var planSpec in planSpecs)
            {
                if (!await db.MealPlans.AnyAsync(
                    x => x.OutletId == outlet.Id && x.Name == planSpec.Item1, ct))
                {
                    db.MealPlans.Add(new MealPlan
                    {
                        Id = Guid.NewGuid(),
                        OutletId = outlet.Id,
                        Name = planSpec.Item1,
                        Frequency = planSpec.Item2,
                        MealsPerDay = planSpec.Item3,
                        MealsPerWeek = planSpec.Item4,
                        Price = planSpec.Item5,
                        Currency = "INR",
                        Description = planSpec.Item6,
                        IsActive = true
                    });
                }
            }

            await db.SaveChangesAsync(ct);

            var ingredients = await db.Ingredients
                .GroupBy(x => x.Name)
                .Select(g => g.First())
                .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, ct);

            var recipes = spec.City == "Chennai"
                ? new[]
                {
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Andhra Full Meals", Calories = 720, ProteinGrams = 20, CarbsGrams = 112, FatGrams = 20,
                        Category = RecipeCategory.Veg, PricePerMeal = 180, LargePricePerMeal = 220,
                        Description = "Traditional Andhra-style rice meal with pappu, vegetable curry, rasam, pickle, curd and papad.",
                        ImageUrl = "https://media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_300%2Ch_300%2Cc_fit/FOOD_CATALOG/IMAGES/CMS/2024/5/20/736b159e-4973-43ed-bd14-fdece423f486_b24651c4-c34c-4801-a94a-cd8b95261ca4.jpg",
                        Tags = "Andhra,Meals,Veg,South Indian"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Chapathi with Andhra Chicken Curry", Calories = 690, ProteinGrams = 43, CarbsGrams = 61, FatGrams = 28,
                        Category = RecipeCategory.NonVeg, PricePerMeal = 220, LargePricePerMeal = 270,
                        Description = "Soft whole-wheat chapathi served with spicy Andhra chicken curry and onion salad.",
                        ImageUrl = "https://media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_400%2Ch_400/FOOD_CATALOG/IMAGES/CMS/2026/1/22/51dcca7c-5a87-44ce-b84a-65dc2865cc86_eb7b228f-342d-402b-885b-607d55dd46fc.png",
                        Tags = "Andhra,Chicken,Chapathi,High Protein"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Rice with Andhra Fish Curry", Calories = 640, ProteinGrams = 38, CarbsGrams = 72, FatGrams = 19,
                        Category = RecipeCategory.NonVeg, PricePerMeal = 240, LargePricePerMeal = 290,
                        Description = "Steamed rice paired with tangy, spicy fish curry cooked with curry leaves and tamarind.",
                        ImageUrl = "https://images.deliveryhero.io/image/fd-bd/LH/lcte-listing.jpg",
                        Tags = "Andhra,Fish,Seafood,Rice"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Masala Dosa with Coconut Chutney", Calories = 470, ProteinGrams = 11, CarbsGrams = 70, FatGrams = 14,
                        Category = RecipeCategory.Veg, PricePerMeal = 140, LargePricePerMeal = 170,
                        Description = "Crisp fermented rice-lentil dosa filled with spiced potato masala, sambar and chutneys.",
                        ImageUrl = "https://dineout-media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_600%2Ch_468/v1669037563/pz1plv3qopsdgu5lyuuw.jpg",
                        Tags = "Dosa,Breakfast,South Indian,Veg"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Idli, Sambar and Peanut Chutney", Calories = 430, ProteinGrams = 13, CarbsGrams = 68, FatGrams = 10,
                        Category = RecipeCategory.Veg, PricePerMeal = 120, LargePricePerMeal = 150,
                        Description = "Steamed rice-and-urad idlis with sambar and a classic peanut chutney.",
                        ImageUrl = "https://media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_300%2Ch_300%2Cc_fit/FOOD_CATALOG/IMAGES/CMS/2024/5/20/736b159e-4973-43ed-bd14-fdece423f486_b24651c4-c34c-4801-a94a-cd8b95261ca4.jpg",
                        Tags = "Idli,Sambar,Breakfast,South Indian"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Andhra Chicken Curry with Rice", Calories = 650, ProteinGrams = 45, CarbsGrams = 67, FatGrams = 21,
                        Category = RecipeCategory.NonVeg, PricePerMeal = 230, LargePricePerMeal = 280,
                        Description = "Bold Andhra chicken curry with steamed rice, onions and fresh coriander.",
                        ImageUrl = "https://media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_400/vvxcyrz7fism7kye5nqq",
                        Tags = "Andhra,Chicken,Rice,NonVeg"
                    }
                }
                : new[]
                {
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Delhi Veg Thali", Calories = 730, ProteinGrams = 24, CarbsGrams = 101, FatGrams = 27,
                        Category = RecipeCategory.Veg, PricePerMeal = 190, LargePricePerMeal = 235,
                        Description = "North Indian thali with dal, seasonal vegetables, paneer, jeera rice, roti and raita.",
                        ImageUrl = "https://masalapolska.com/assets/indian_thali_meal_top_down-BK7jUhaG.png",
                        Tags = "Delhi,Thali,Veg,North Indian"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Butter Chicken with Garlic Naan", Calories = 820, ProteinGrams = 46, CarbsGrams = 69, FatGrams = 38,
                        Category = RecipeCategory.NonVeg, PricePerMeal = 260, LargePricePerMeal = 320,
                        Description = "Tandoori chicken simmered in a creamy tomato-butter sauce with garlic naan.",
                        ImageUrl = "https://dickson.tajagra.com.au/wp-content/uploads/sites/3/2021/05/Chicken-Makhani-Butter-Chicken.jpg",
                        Tags = "Butter Chicken,Naan,North Indian,High Protein"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Dal Makhani with Butter Naan", Calories = 690, ProteinGrams = 22, CarbsGrams = 86, FatGrams = 27,
                        Category = RecipeCategory.Veg, PricePerMeal = 190, LargePricePerMeal = 235,
                        Description = "Slow-cooked black lentils and kidney beans served with buttery naan.",
                        ImageUrl = "https://media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_300%2Ch_300%2Cc_fit/t6mwawa7mdm6lql1clmc",
                        Tags = "Dal Makhani,Naan,Veg,North Indian"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Paneer Tikka Masala with Roti", Calories = 650, ProteinGrams = 28, CarbsGrams = 62, FatGrams = 31,
                        Category = RecipeCategory.Veg, PricePerMeal = 220, LargePricePerMeal = 270,
                        Description = "Charred paneer and peppers in a spiced tomato gravy with whole-wheat roti.",
                        ImageUrl = "https://media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_300%2Ch_300%2Ce_grayscale%2Cc_fit/FOOD_CATALOG/IMAGES/CMS/2025/7/29/dcf726ba-6e7b-42bf-95c3-6f1e103db9da_a94a8054-7c2a-4cbb-902e-dd37ffb92d61.png",
                        Tags = "Paneer,Tikka,Roti,Veg"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Chole Bhature", Calories = 760, ProteinGrams = 20, CarbsGrams = 101, FatGrams = 29,
                        Category = RecipeCategory.Veg, PricePerMeal = 180, LargePricePerMeal = 225,
                        Description = "Punjabi-style spiced chickpeas with fluffy bhature, onion and lemon.",
                        ImageUrl = "https://media-assets.swiggy.com/swiggy/image/upload/fl_lossy%2Cf_auto%2Cq_auto%2Cw_400/RX_THUMBNAIL/IMAGES/VENDOR/2025/9/4/9e90029f-0af2-49eb-b92b-25f32ff57157_1167777%20%281%29.jpg",
                        Tags = "Chole,Bhature,Punjabi,Veg"
                    },
                    new Recipe
                    {
                        Id = Guid.NewGuid(), OutletId = outlet.Id, Name = "Chicken Biryani with Raita", Calories = 790, ProteinGrams = 44, CarbsGrams = 91, FatGrams = 26,
                        Category = RecipeCategory.NonVeg, PricePerMeal = 260, LargePricePerMeal = 320,
                        Description = "Fragrant basmati rice layered with spiced chicken, fried onions, mint and raita.",
                        ImageUrl = "https://media-assets.swiggy.com/swiggy/image/upload/f_auto%2Cq_auto%2Cfl_lossy/RX_THUMBNAIL/IMAGES/VENDOR/2025/9/22/97c6822e-e46f-418a-b962-481220341835_1209291.jpg",
                        Tags = "Biryani,Chicken,North Indian,High Protein"
                    }
                };

            if (spec.Slug == "hyderabad-zaika")
            {
                recipes = new[]
                {
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Hyderabadi Chicken Biryani", Calories=780, ProteinGrams=44, CarbsGrams=91, FatGrams=25, Category=RecipeCategory.NonVeg, PricePerMeal=260, LargePricePerMeal=320, Description="Aromatic basmati rice layered with spiced chicken, saffron, mint and fried onions.", ImageUrl="https://images.unsplash.com/photo-1563379091339-03246963d29a?auto=format&fit=crop&w=900&q=85", Tags="Hyderabadi,Biryani,Chicken,High Protein" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Mutton Hyderabadi Biryani", Calories=860, ProteinGrams=42, CarbsGrams=89, FatGrams=34, Category=RecipeCategory.NonVeg, PricePerMeal=300, LargePricePerMeal=360, Description="Slow-cooked mutton and fragrant basmati rice finished with mint and saffron.", ImageUrl="https://images.unsplash.com/photo-1599043513942-a7b2c4c2e4be?auto=format&fit=crop&w=900&q=85", Tags="Hyderabadi,Biryani,Mutton" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Hyderabadi Haleem", Calories=690, ProteinGrams=38, CarbsGrams=58, FatGrams=30, Category=RecipeCategory.NonVeg, PricePerMeal=240, LargePricePerMeal=290, Description="Slow-cooked meat, lentils and wheat blended with aromatic Hyderabad spices.", ImageUrl="https://images.unsplash.com/photo-1601050690117-94f5f6fa8bd7?auto=format&fit=crop&w=900&q=85", Tags="Haleem,Hyderabadi,Mutton" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Chicken 65 with Jeera Rice", Calories=710, ProteinGrams=41, CarbsGrams=68, FatGrams=27, Category=RecipeCategory.NonVeg, PricePerMeal=230, LargePricePerMeal=280, Description="Crisp Hyderabad-style chicken with cumin basmati rice and onion salad.", ImageUrl="https://images.unsplash.com/photo-1601050690117-94f5f6fa8bd7?auto=format&fit=crop&w=900&q=85", Tags="Chicken65,Rice,Hyderabadi" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Bagara Rice with Mirchi Ka Salan", Calories=560, ProteinGrams=12, CarbsGrams=79, FatGrams=19, Category=RecipeCategory.Veg, PricePerMeal=170, LargePricePerMeal=210, Description="Hyderabadi spiced rice served with tangy peanut-sesame chilli curry.", ImageUrl="https://images.unsplash.com/photo-1596797038530-2c107229654b?auto=format&fit=crop&w=900&q=85", Tags="Bagara Rice,Mirchi Ka Salan,Veg" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Double Ka Meetha", Calories=420, ProteinGrams=9, CarbsGrams=54, FatGrams=18, Category=RecipeCategory.Veg, PricePerMeal=110, LargePricePerMeal=140, Description="Hyderabadi bread pudding with milk, saffron and nuts.", ImageUrl="https://images.unsplash.com/photo-1601050690597-df0568f70950?auto=format&fit=crop&w=900&q=85", Tags="Dessert,Hyderabadi" }
                };
            }
            else if (spec.Slug == "deccan-wok")
            {
                recipes = new[]
                {
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Chicken Hakka Noodles", Calories=650, ProteinGrams=36, CarbsGrams=77, FatGrams=22, Category=RecipeCategory.NonVeg, PricePerMeal=220, LargePricePerMeal=270, Description="Wok-tossed noodles with chicken, cabbage, peppers, spring onion and sauces.", ImageUrl="https://images.unsplash.com/photo-1569718212165-3a8278d5f624?auto=format&fit=crop&w=900&q=85", Tags="Indo-Chinese,Noodles,Chicken" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Veg Hakka Noodles", Calories=540, ProteinGrams=16, CarbsGrams=79, FatGrams=17, Category=RecipeCategory.Veg, PricePerMeal=180, LargePricePerMeal=220, Description="Classic vegetable Hakka noodles finished in a hot wok.", ImageUrl="https://images.unsplash.com/photo-1569718212165-3a8278d5f624?auto=format&fit=crop&w=900&q=85", Tags="Indo-Chinese,Noodles,Veg" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Schezwan Chicken Fried Rice", Calories=690, ProteinGrams=35, CarbsGrams=82, FatGrams=23, Category=RecipeCategory.NonVeg, PricePerMeal=220, LargePricePerMeal=270, Description="Basmati rice wok-fried with chicken, vegetables and spicy Schezwan sauce.", ImageUrl="https://images.unsplash.com/photo-1603133872878-684f208fb84b?auto=format&fit=crop&w=900&q=85", Tags="Indo-Chinese,Fried Rice,Schezwan" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Paneer Chilli with Fried Rice", Calories=720, ProteinGrams=28, CarbsGrams=82, FatGrams=27, Category=RecipeCategory.Veg, PricePerMeal=210, LargePricePerMeal=260, Description="Crisp paneer with bell peppers and onions in a chilli-soy glaze, served with fried rice.", ImageUrl="https://images.unsplash.com/photo-1603133872878-684f208fb84b?auto=format&fit=crop&w=900&q=85", Tags="Indo-Chinese,Paneer,Fried Rice" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Dragon Chicken", Calories=670, ProteinGrams=43, CarbsGrams=46, FatGrams=28, Category=RecipeCategory.NonVeg, PricePerMeal=240, LargePricePerMeal=290, Description="Crisp chicken tossed with dried chillies, peppers, spring onion and a glossy sauce.", ImageUrl="https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=900&q=85", Tags="Indo-Chinese,Chicken,Spicy" },
                    new Recipe { Id=Guid.NewGuid(), OutletId=outlet.Id, Name="Veg Manchurian with Fried Rice", Calories=610, ProteinGrams=18, CarbsGrams=84, FatGrams=20, Category=RecipeCategory.Veg, PricePerMeal=190, LargePricePerMeal=235, Description="Vegetable Manchurian balls in savoury sauce with wok-fried rice.", ImageUrl="https://images.unsplash.com/photo-1603133872878-684f208fb84b?auto=format&fit=crop&w=900&q=85", Tags="Indo-Chinese,Manchurian,Veg" }
                };
            }

            var catalogRecipes = new List<Recipe>();

            foreach (var newRecipe in recipes)
            {
                var recipe = await db.Recipes.FirstOrDefaultAsync(
                    x => x.OutletId == outlet.Id && x.Name == newRecipe.Name, ct);

                if (recipe is null)
                {
                    recipe = newRecipe;
                    db.Recipes.Add(recipe);
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    recipe.Calories = newRecipe.Calories;
                    recipe.ProteinGrams = newRecipe.ProteinGrams;
                    recipe.CarbsGrams = newRecipe.CarbsGrams;
                    recipe.FatGrams = newRecipe.FatGrams;
                    recipe.Category = newRecipe.Category;
                    recipe.PricePerMeal = newRecipe.PricePerMeal;
                    recipe.LargePricePerMeal = newRecipe.LargePricePerMeal;
                    recipe.Description = newRecipe.Description;
                    recipe.ImageUrl = newRecipe.ImageUrl;
                    recipe.Tags = newRecipe.Tags;
                    recipe.IsActive = true;
                }

                catalogRecipes.Add(recipe);

                var wantedIngredients = spec.City == "Chennai"
                    ? new Dictionary<string, (string Name, decimal Quantity, string Unit)[]>
                    {
                        ["Andhra Full Meals"] = new[] { ("White Rice", 220m, "g"), ("Toor Dal", 100m, "g"), ("Potato", 70m, "g"), ("Tomato", 60m, "g"), ("Yogurt", 80m, "g"), ("Ghee", 8m, "g") },
                        ["Chapathi with Andhra Chicken Curry"] = new[] { ("Whole Wheat Flour", 90m, "g"), ("Chicken Thigh", 150m, "g"), ("Onion", 80m, "g"), ("Tomato", 80m, "g"), ("Ginger Garlic Paste", 15m, "g"), ("Red Chili Powder", 5m, "g"), ("Coriander", 5m, "g"), ("Garam Masala", 4m, "g"), ("Olive Oil", 10m, "g") },
                        ["Rice with Andhra Fish Curry"] = new[] { ("White Rice", 220m, "g"), ("Fish Fillet", 160m, "g"), ("Tamarind", 20m, "g"), ("Onion", 60m, "g"), ("Tomato", 60m, "g"), ("Curry Leaves", 4m, "g"), ("Red Chili Powder", 5m, "g"), ("Sesame Oil", 10m, "g") },
                        ["Masala Dosa with Coconut Chutney"] = new[] { ("Dosa Batter", 180m, "g"), ("Potato", 100m, "g"), ("Onion", 40m, "g"), ("Mustard Seeds", 3m, "g"), ("Curry Leaves", 3m, "g"), ("Coconut", 30m, "g"), ("Olive Oil", 8m, "g") },
                        ["Idli, Sambar and Peanut Chutney"] = new[] { ("Idli Batter", 180m, "g"), ("Toor Dal", 70m, "g"), ("Carrot", 40m, "g"), ("Tomato", 50m, "g"), ("Peanuts", 25m, "g"), ("Coconut", 15m, "g"), ("Olive Oil", 6m, "g") },
                        ["Andhra Chicken Curry with Rice"] = new[] { ("White Rice", 220m, "g"), ("Chicken Thigh", 160m, "g"), ("Onion", 70m, "g"), ("Ginger Garlic Paste", 15m, "g"), ("Garam Masala", 4m, "g"), ("Red Chili Powder", 6m, "g"), ("Coriander", 5m, "g"), ("Olive Oil", 10m, "g") }
                    }
                    : new Dictionary<string, (string Name, decimal Quantity, string Unit)[]>
                    {
                        ["Delhi Veg Thali"] = new[] { ("Basmati Rice", 180m, "g"), ("Toor Dal", 80m, "g"), ("Paneer", 80m, "g"), ("Whole Wheat Flour", 70m, "g"), ("Yogurt", 80m, "g"), ("Mixed Vegetables", 90m, "g"), ("Ghee", 8m, "g") },
                        ["Butter Chicken with Garlic Naan"] = new[] { ("Chicken Thigh", 160m, "g"), ("Yogurt", 60m, "g"), ("Butter", 15m, "g"), ("Cream", 35m, "g"), ("Tomato", 100m, "g"), ("Cashews", 20m, "g"), ("Whole Wheat Flour", 90m, "g"), ("Garam Masala", 4m, "g") },
                        ["Dal Makhani with Butter Naan"] = new[] { ("Black Lentils", 110m, "g"), ("Kidney Beans", 70m, "g"), ("Butter", 12m, "g"), ("Cream", 25m, "g"), ("Whole Wheat Flour", 90m, "g"), ("Tomato", 70m, "g"), ("Garam Masala", 4m, "g") },
                        ["Paneer Tikka Masala with Roti"] = new[] { ("Paneer", 140m, "g"), ("Yogurt", 60m, "g"), ("Bell Pepper", 60m, "g"), ("Tomato", 100m, "g"), ("Onion", 60m, "g"), ("Whole Wheat Flour", 80m, "g"), ("Garam Masala", 4m, "g"), ("Olive Oil", 8m, "g") },
                        ["Chole Bhature"] = new[] { ("Chickpeas", 150m, "g"), ("Whole Wheat Flour", 110m, "g"), ("Yogurt", 50m, "g"), ("Onion", 50m, "g"), ("Tomato", 60m, "g"), ("Garam Masala", 4m, "g"), ("Olive Oil", 12m, "g") },
                        ["Chicken Biryani with Raita"] = new[] { ("Basmati Rice", 220m, "g"), ("Chicken Thigh", 160m, "g"), ("Yogurt", 70m, "g"), ("Onion", 60m, "g"), ("Saffron", 1m, "g"), ("Garam Masala", 4m, "g"), ("Cashews", 15m, "g"), ("Ghee", 10m, "g") }
                    };

                if (spec.Slug == "hyderabad-zaika")
                {
                    wantedIngredients = new Dictionary<string, (string Name, decimal Quantity, string Unit)[]>
                    {
                        ["Hyderabadi Chicken Biryani"] = new[] { ("Basmati Rice", 220m, "g"), ("Chicken Thigh", 160m, "g"), ("Yogurt", 70m, "g"), ("Onion", 60m, "g"), ("Saffron", 1m, "g"), ("Biryani Masala", 6m, "g"), ("Mint", 6m, "g"), ("Ghee", 10m, "g") },
                        ["Mutton Hyderabadi Biryani"] = new[] { ("Basmati Rice", 220m, "g"), ("Mutton", 170m, "g"), ("Yogurt", 70m, "g"), ("Onion", 60m, "g"), ("Saffron", 1m, "g"), ("Biryani Masala", 7m, "g"), ("Mint", 6m, "g"), ("Ghee", 12m, "g") },
                        ["Hyderabadi Haleem"] = new[] { ("Mutton", 150m, "g"), ("Wheat", 60m, "g"), ("Lentils", 90m, "g"), ("Ginger Garlic Paste", 15m, "g"), ("Ghee", 10m, "g"), ("Mint", 5m, "g"), ("Garam Masala", 4m, "g") },
                        ["Chicken 65 with Jeera Rice"] = new[] { ("Chicken Thigh", 160m, "g"), ("Basmati Rice", 180m, "g"), ("Corn Flour", 20m, "g"), ("Yogurt", 30m, "g"), ("Red Chili Powder", 5m, "g"), ("Curry Leaves", 4m, "g"), ("Olive Oil", 12m, "g") },
                        ["Bagara Rice with Mirchi Ka Salan"] = new[] { ("Basmati Rice", 200m, "g"), ("Peanuts", 20m, "g"), ("Sesame Seeds", 15m, "g"), ("Coconut", 20m, "g"), ("Green Chilies", 45m, "g"), ("Onion", 50m, "g"), ("Tomato", 50m, "g"), ("Sesame Oil", 10m, "g") },
                        ["Double Ka Meetha"] = new[] { ("Bread", 100m, "g"), ("Milk", 100m, "g"), ("Sugar", 15m, "g"), ("Ghee", 10m, "g"), ("Cashews", 12m, "g"), ("Saffron", 1m, "g") }
                    };
                }
                else if (spec.Slug == "deccan-wok")
                {
                    wantedIngredients = new Dictionary<string, (string Name, decimal Quantity, string Unit)[]>
                    {
                        ["Chicken Hakka Noodles"] = new[] { ("Wheat Noodles", 170m, "g"), ("Chicken Breast", 130m, "g"), ("Cabbage", 50m, "g"), ("Carrot", 40m, "g"), ("Bell Pepper", 40m, "g"), ("Spring Onion", 20m, "g"), ("Soy Sauce", 15m, "g"), ("Vinegar", 8m, "g"), ("Sesame Oil", 8m, "g") },
                        ["Veg Hakka Noodles"] = new[] { ("Wheat Noodles", 180m, "g"), ("Cabbage", 60m, "g"), ("Carrot", 50m, "g"), ("Bell Pepper", 50m, "g"), ("Spring Onion", 20m, "g"), ("Soy Sauce", 15m, "g"), ("Vinegar", 8m, "g"), ("Sesame Oil", 8m, "g") },
                        ["Schezwan Chicken Fried Rice"] = new[] { ("Basmati Rice", 210m, "g"), ("Chicken Breast", 130m, "g"), ("Carrot", 40m, "g"), ("Bell Pepper", 40m, "g"), ("Spring Onion", 20m, "g"), ("Schezwan Sauce", 25m, "g"), ("Soy Sauce", 12m, "g"), ("Sesame Oil", 8m, "g") },
                        ["Paneer Chilli with Fried Rice"] = new[] { ("Paneer", 130m, "g"), ("Basmati Rice", 180m, "g"), ("Bell Pepper", 60m, "g"), ("Onion", 50m, "g"), ("Spring Onion", 20m, "g"), ("Soy Sauce", 12m, "g"), ("Corn Flour", 15m, "g"), ("Sesame Oil", 8m, "g") },
                        ["Dragon Chicken"] = new[] { ("Chicken Breast", 160m, "g"), ("Corn Flour", 20m, "g"), ("Bell Pepper", 50m, "g"), ("Onion", 50m, "g"), ("Spring Onion", 20m, "g"), ("Soy Sauce", 15m, "g"), ("Chili Sauce", 20m, "g"), ("Sesame Oil", 8m, "g") },
                        ["Veg Manchurian with Fried Rice"] = new[] { ("Basmati Rice", 180m, "g"), ("Mixed Vegetables", 100m, "g"), ("Corn Flour", 25m, "g"), ("Cabbage", 40m, "g"), ("Bell Pepper", 40m, "g"), ("Soy Sauce", 12m, "g"), ("Spring Onion", 20m, "g"), ("Sesame Oil", 8m, "g") }
                    };
                }

                if (wantedIngredients.TryGetValue(recipe.Name, out var ingredientSpecs))
                {
                    foreach (var item in ingredientSpecs)
                    {
                        if (!ingredients.TryGetValue(item.Name, out var ingredient))
                            continue;

                        if (!await db.RecipeIngredients.AnyAsync(
                            x => x.RecipeId == recipe.Id && x.IngredientId == ingredient.Id, ct))
                        {
                            db.RecipeIngredients.Add(new RecipeIngredient
                            {
                                Id = Guid.NewGuid(),
                                RecipeId = recipe.Id,
                                IngredientId = ingredient.Id,
                                Quantity = item.Quantity,
                                Unit = item.Unit
                            });
                        }
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            var menuRecipes = catalogRecipes;

            var breakfast = menuRecipes.Where(x => new[] { "Masala Dosa with Coconut Chutney", "Idli, Sambar and Peanut Chutney" }.Contains(x.Name)).ToList();
            var lunch = menuRecipes.Where(x => new[] { "Andhra Full Meals", "Rice with Andhra Fish Curry", "Delhi Veg Thali", "Dal Makhani with Butter Naan", "Chicken Biryani with Raita" }.Contains(x.Name)).ToList();
            var dinner = menuRecipes.Where(x => new[] { "Chapathi with Andhra Chicken Curry", "Andhra Chicken Curry with Rice", "Butter Chicken with Garlic Naan", "Paneer Tikka Masala with Roti", "Chole Bhature" }.Contains(x.Name)).ToList();

            if (spec.Slug == "hyderabad-zaika")
            {
                breakfast = menuRecipes.Where(x => new[] { "Double Ka Meetha" }.Contains(x.Name)).ToList();
                lunch = menuRecipes.Where(x => new[] { "Hyderabadi Chicken Biryani", "Mutton Hyderabadi Biryani", "Bagara Rice with Mirchi Ka Salan" }.Contains(x.Name)).ToList();
                dinner = menuRecipes.Where(x => new[] { "Hyderabadi Haleem", "Chicken 65 with Jeera Rice" }.Contains(x.Name)).ToList();
            }
            else if (spec.Slug == "deccan-wok")
            {
                breakfast = menuRecipes.Where(x => new[] { "Veg Hakka Noodles" }.Contains(x.Name)).ToList();
                lunch = menuRecipes.Where(x => new[] { "Schezwan Chicken Fried Rice", "Paneer Chilli with Fried Rice", "Veg Manchurian with Fried Rice" }.Contains(x.Name)).ToList();
                dinner = menuRecipes.Where(x => new[] { "Chicken Hakka Noodles", "Dragon Chicken" }.Contains(x.Name)).ToList();
            }

            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                foreach (var recipe in breakfast.Where(x => x.IsActive).Take(2))
                    await EnsureMenuItemAsync(db, outlet.Id, recipe.Id, day, MealSlot.Morning, ct);

                foreach (var recipe in lunch.Where(x => x.IsActive).Take(3))
                    await EnsureMenuItemAsync(db, outlet.Id, recipe.Id, day, MealSlot.Afternoon, ct);

                foreach (var recipe in dinner.Where(x => x.IsActive).Take(2))
                    await EnsureMenuItemAsync(db, outlet.Id, recipe.Id, day, MealSlot.Evening, ct);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureMenuItemAsync(
        HealthAppDbContext db, Guid outletId, Guid recipeId, DayOfWeek day, MealSlot slot, CancellationToken ct)
    {
        if (await db.OutletMenuItems.AnyAsync(
            x => x.OutletId == outletId && x.RecipeId == recipeId && x.DayOfWeek == day && x.MealSlot == slot, ct))
            return;

        db.OutletMenuItems.Add(new OutletMenuItem
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            RecipeId = recipeId,
            DayOfWeek = day,
            MealSlot = slot,
            IsAvailable = true,
            DisplayOrder = 1
        });
    }

    private static async Task EnsureExistingOutletMenuSlotsAsync(HealthAppDbContext db,CancellationToken ct)
    {
        var fit=await db.Outlets.AsNoTracking().FirstOrDefaultAsync(x=>x.Slug=="fitfood",ct);
        if(fit is null)return;

        var recipes=await db.Recipes.AsNoTracking()
            .Where(x=>x.OutletId==fit.Id)
            .GroupBy(x=>x.Name)
            .Select(g=>g.First())
            .ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        var slotRecipes=new Dictionary<MealSlot,string>
        {
            [MealSlot.Morning]="Paneer Power Bowl",
            [MealSlot.Afternoon]="Chicken Tikka Bowl",
            [MealSlot.Evening]="Prawn Noodles",
            [MealSlot.Night]="Chickpea Buddha Bowl"
        };

        foreach(var day in new[]{DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday,DayOfWeek.Saturday})
        foreach(var item in slotRecipes)
        {
            if(!recipes.TryGetValue(item.Value,out var recipe))continue;
            var exists=await db.OutletMenuItems.AnyAsync(x=>x.OutletId==fit.Id&&x.DayOfWeek==day&&x.MealSlot==item.Key&&x.RecipeId==recipe.Id,ct);
            if(exists)continue;
            db.OutletMenuItems.Add(new OutletMenuItem{Id=Guid.NewGuid(),OutletId=fit.Id,RecipeId=recipe.Id,DayOfWeek=day,MealSlot=item.Key,IsAvailable=true,DisplayOrder=1});
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCatalogAsync(HealthAppDbContext db,CancellationToken ct)
    {
        var allergenNames=new[] {
            "Milk",
            "Egg",
            "Peanuts",
            "Tree Nuts",
            "Soy",
            "Wheat/Gluten",
            "Sesame",
            "Fish",
            "Shellfish"
        };
        var allergens=await db.Allergens.ToListAsync(ct);
        foreach(var name in allergenNames)
        if(!allergens.Any(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))
        db.Allergens.Add(new Allergen {
            Id=Guid.NewGuid(),Name=name
        });
        var ingredientNames=new[] {
            ("Chicken Breast","g"),
            ("Olive Oil","g"),
            ("Paneer","g"),
            ("Brown Rice","g"),
            ("Broccoli","g"),
            ("Chickpeas","g"),
            ("Tahini","g"),
            ("Lentils","g"),
            ("Prawns","g"),
            ("Wheat Noodles","g"),
            ("Mutton","g"),
            ("White Rice","g"),
            ("Sona Masoori Rice","g"),
            ("Basmati Rice","g"),
            ("Basmati Rice (Boiled)","g"),
            ("Chicken Breast (Grilled)","g"),
            ("Whole Wheat Flour","g"),
            ("Toor Dal","g"),
            ("Black Lentils","g"),
            ("Kidney Beans","g"),
            ("Idli Batter","g"),
            ("Dosa Batter","g"),
            ("Potato","g"),
            ("Tomato","g"),
            ("Onion","g"),
            ("Carrot","g"),
            ("Mixed Vegetables","g"),
            ("Bell Pepper","g"),
            ("Ginger Garlic Paste","g"),
            ("Green Chilies","g"),
            ("Curry Leaves","g"),
            ("Coriander","g"),
            ("Tamarind","g"),
            ("Coconut","g"),
            ("Mustard Seeds","g"),
            ("Red Chili Powder","g"),
            ("Garam Masala","g"),
            ("Sesame Oil","g"),
            ("Yogurt","g"),
            ("Ghee","g"),
            ("Butter","g"),
            ("Cream","g"),
            ("Cashews","g"),
            ("Peanuts","g"),
            ("Fish Fillet","g"),
            ("Chicken Thigh","g"),
            ("Saffron","g"),
            ("Biryani Masala","g"),
            ("Mint","g"),
            ("Wheat","g"),
            ("Corn Flour","g"),
            ("Sesame Seeds","g"),
            ("Green Chilies","g"),
            ("Bread","g"),
            ("Milk","g"),
            ("Sugar","g"),
            ("Cabbage","g"),
            ("Carrot","g"),
            ("Bell Pepper","g"),
            ("Spring Onion","g"),
            ("Soy Sauce","g"),
            ("Vinegar","g"),
            ("Schezwan Sauce","g"),
            ("Chili Sauce","g")
        };
        var ingredients=await db.Ingredients.ToListAsync(ct);
        foreach(var item in ingredientNames.GroupBy(x=>x.Item1,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))
        if(!ingredients.Any(x=>x.Name.Equals(item.Item1,StringComparison.OrdinalIgnoreCase)))
        db.Ingredients.Add(new Ingredient {
            Id=Guid.NewGuid(),Name=item.Item1,DefaultUnit=item.Item2
        });
        await db.SaveChangesAsync(ct);

        // Populate the static nutrition reference values after the ingredient catalog
        // exists. Existing rows are updated idempotently on each application initialization.
        await NutritionReferenceData.ApplyAsync(db, ct);

        var all=await db.Allergens.ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        var ing=await db.Ingredients
            .GroupBy(x=>x.Name)
            .Select(g=>g.First())
            .ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        LinkIngredientAllergen(db,ing["Paneer"],all["Milk"]);
        LinkIngredientAllergen(db,ing["Tahini"],all["Sesame"]);
        LinkIngredientAllergen(db,ing["Prawns"],all["Shellfish"]);
        LinkIngredientAllergen(db,ing["Wheat Noodles"],all["Wheat/Gluten"]);
        LinkIngredientAllergen(db,ing["Whole Wheat Flour"],all["Wheat/Gluten"]);
        LinkIngredientAllergen(db,ing["Yogurt"],all["Milk"]);
        LinkIngredientAllergen(db,ing["Ghee"],all["Milk"]);
        LinkIngredientAllergen(db,ing["Butter"],all["Milk"]);
        LinkIngredientAllergen(db,ing["Cream"],all["Milk"]);
        LinkIngredientAllergen(db,ing["Cashews"],all["Tree Nuts"]);
        LinkIngredientAllergen(db,ing["Peanuts"],all["Peanuts"]);
        LinkIngredientAllergen(db,ing["Fish Fillet"],all["Fish"]);
        LinkIngredientAllergen(db,ing["Wheat"],all["Wheat/Gluten"]);
        LinkIngredientAllergen(db,ing["Bread"],all["Wheat/Gluten"]);
        LinkIngredientAllergen(db,ing["Milk"],all["Milk"]);
        LinkIngredientAllergen(db,ing["Sesame Seeds"],all["Sesame"]);
        LinkIngredientAllergen(db,ing["Soy Sauce"],all["Soy"]);
        LinkIngredientAllergen(db,ing["Soy Sauce"],all["Wheat/Gluten"]);
        await db.SaveChangesAsync(ct);
    }
    private static void LinkIngredientAllergen(HealthAppDbContext db,Ingredient ingredient,Allergen allergen)
    {
        if(!db.IngredientAllergens.Any(x=>x.IngredientId==ingredient.Id&&x.AllergenId==allergen.Id))
        db.IngredientAllergens.Add(new IngredientAllergen {
            IngredientId=ingredient.Id,AllergenId=allergen.Id
        });
    }
    private static void AddRecipeIngredients(Recipe recipe,(string Name,decimal Quantity,string Unit)[] items,Dictionary<string,Ingredient> ingredients)
    {
        foreach(var item in items)
        {
            var ingredient=ingredients[item.Name];
            recipe.RecipeIngredients.Add(new RecipeIngredient {
                Id=Guid.NewGuid(),RecipeId=recipe.Id,IngredientId=ingredient.Id,Quantity=item.Quantity,Unit=item.Unit
            });
        }
    }
    private static async Task EnsureExistingRecipeCatalogLinksAsync(HealthAppDbContext db,CancellationToken ct)
    {
        var recipes=await db.Recipes.AsNoTracking().ToListAsync(ct);
        var ingredients=await db.Ingredients.AsNoTracking()
            .GroupBy(x=>x.Name)
            .Select(g=>g.First())
            .ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
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
            if(!wanted.TryGetValue(recipe.Name,out var items))
                continue;

            foreach(var item in items)
            {
                if(!ingredients.TryGetValue(item.Name,out var ingredient))
                    continue;

                await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (
    SELECT 1
    FROM dbo.RecipeIngredients
    WHERE RecipeId={recipe.Id} AND IngredientId={ingredient.Id}
)
BEGIN
    INSERT INTO dbo.RecipeIngredients
        (Id,RecipeId,IngredientId,Quantity,Unit)
    VALUES
        ({Guid.NewGuid()},{recipe.Id},{ingredient.Id},{item.Quantity},{item.Unit});
END",ct);
            }
        }

        var customer=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Email=="customer@healthapp.test",ct);
        if(customer is null)
            return;

        var all=await db.Allergens.AsNoTracking().ToDictionaryAsync(x=>x.Name,StringComparer.OrdinalIgnoreCase,ct);
        foreach(var name in new[] {"Milk","Shellfish"})
        {
            if(!all.TryGetValue(name,out var allergen))
                continue;

            await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (
    SELECT 1
    FROM dbo.CustomerAllergies
    WHERE CustomerId={customer.Id} AND AllergenId={allergen.Id}
)
BEGIN
    INSERT INTO dbo.CustomerAllergies
        (Id,CustomerId,AllergenId)
    VALUES
        ({Guid.NewGuid()},{customer.Id},{allergen.Id});
END",ct);
        }
    }
}