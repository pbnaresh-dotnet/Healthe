using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace HealthApp.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthAppDbContext>();
        // The checked-in demo package is self-contained. A production deployment should
        // replace EnsureCreatedAsync with EF Core MigrateAsync after generating migrations.
        await db.Database.EnsureCreatedAsync(cancellationToken);
        // Development/demo compatibility: normalized catalog tables are created explicitly because
        // EnsureCreatedAsync does not evolve an already-existing database.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.ServiceCities','U') IS NULL
BEGIN
    CREATE TABLE dbo.ServiceCities(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ServiceCities PRIMARY KEY,
        City nvarchar(100) NOT NULL,
        State nvarchar(100) NOT NULL,
        Country nvarchar(100) NOT NULL,
        Latitude float NOT NULL,
        Longitude float NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_ServiceCities_IsEnabled DEFAULT 1
    );
    CREATE UNIQUE INDEX IX_ServiceCities_City ON dbo.ServiceCities(City);
END;
IF COL_LENGTH('dbo.CustomerAddresses','City') IS NULL
    ALTER TABLE dbo.CustomerAddresses ADD City nvarchar(100) NULL;
IF COL_LENGTH('dbo.CustomerAddresses','State') IS NULL
    ALTER TABLE dbo.CustomerAddresses ADD State nvarchar(100) NULL;
IF COL_LENGTH('dbo.CustomerAddresses','Pincode') IS NULL
    ALTER TABLE dbo.CustomerAddresses ADD Pincode nvarchar(20) NULL;
IF COL_LENGTH('dbo.CustomerAddresses','Locality') IS NULL
    ALTER TABLE dbo.CustomerAddresses ADD Locality nvarchar(150) NULL;
IF COL_LENGTH('dbo.CustomerAddresses','CityAreaId') IS NOT NULL
    ALTER TABLE dbo.CustomerAddresses ALTER COLUMN CityAreaId uniqueidentifier NULL;
", cancellationToken);

        // Development/demo compatibility: normalized catalog tables are created explicitly because
        // EnsureCreatedAsync does not evolve an already-existing database.
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.Ingredients','U') IS NULL
BEGIN
    CREATE TABLE dbo.Ingredients(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Ingredients PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        DefaultUnit nvarchar(20) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Ingredients_IsActive DEFAULT 1
    );
    CREATE UNIQUE INDEX IX_Ingredients_Name ON dbo.Ingredients(Name);
END;
IF OBJECT_ID('dbo.Allergens','U') IS NULL
BEGIN
    CREATE TABLE dbo.Allergens(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Allergens PRIMARY KEY,
        Name nvarchar(100) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Allergens_IsActive DEFAULT 1
    );
    CREATE UNIQUE INDEX IX_Allergens_Name ON dbo.Allergens(Name);
END;
IF OBJECT_ID('dbo.RecipeIngredients','U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeIngredients(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_RecipeIngredients PRIMARY KEY,
        RecipeId uniqueidentifier NOT NULL,
        IngredientId uniqueidentifier NOT NULL,
        Quantity decimal(18,3) NOT NULL,
        Unit nvarchar(20) NOT NULL,
        CONSTRAINT FK_RecipeIngredients_Recipes FOREIGN KEY(RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeIngredients_Ingredients FOREIGN KEY(IngredientId) REFERENCES dbo.Ingredients(Id)
    );
    CREATE UNIQUE INDEX IX_RecipeIngredients_Recipe_Ingredient ON dbo.RecipeIngredients(RecipeId,IngredientId);
END;
IF OBJECT_ID('dbo.RecipeAllergens','U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeAllergens(
        RecipeId uniqueidentifier NOT NULL,
        AllergenId uniqueidentifier NOT NULL,
        CONSTRAINT PK_RecipeAllergens PRIMARY KEY(RecipeId,AllergenId),
        CONSTRAINT FK_RecipeAllergens_Recipes FOREIGN KEY(RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeAllergens_Allergens FOREIGN KEY(AllergenId) REFERENCES dbo.Allergens(Id)
    );
END;
IF OBJECT_ID('dbo.IngredientAllergens','U') IS NULL
BEGIN
    CREATE TABLE dbo.IngredientAllergens(
        IngredientId uniqueidentifier NOT NULL,
        AllergenId uniqueidentifier NOT NULL,
        CONSTRAINT PK_IngredientAllergens PRIMARY KEY(IngredientId,AllergenId),
        CONSTRAINT FK_IngredientAllergens_Ingredients FOREIGN KEY(IngredientId) REFERENCES dbo.Ingredients(Id) ON DELETE CASCADE,
        CONSTRAINT FK_IngredientAllergens_Allergens FOREIGN KEY(AllergenId) REFERENCES dbo.Allergens(Id)
    );
END;
IF OBJECT_ID('dbo.CustomerAllergies','U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerAllergies(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CustomerAllergies PRIMARY KEY,
        CustomerId uniqueidentifier NOT NULL,
        AllergenId uniqueidentifier NOT NULL,
        CONSTRAINT FK_CustomerAllergies_Users FOREIGN KEY(CustomerId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CustomerAllergies_Allergens FOREIGN KEY(AllergenId) REFERENCES dbo.Allergens(Id)
    );
    CREATE UNIQUE INDEX IX_CustomerAllergies_Customer_Allergen ON dbo.CustomerAllergies(CustomerId,AllergenId);
END;
IF OBJECT_ID('dbo.CustomerLikedMeals','U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerLikedMeals(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CustomerLikedMeals PRIMARY KEY,
        CustomerId uniqueidentifier NOT NULL,
        RecipeId uniqueidentifier NOT NULL,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_CustomerLikedMeals_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CustomerLikedMeals_Users FOREIGN KEY(CustomerId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CustomerLikedMeals_Recipes FOREIGN KEY(RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_CustomerLikedMeals_Customer_Recipe ON dbo.CustomerLikedMeals(CustomerId,RecipeId);
    CREATE INDEX IX_CustomerLikedMeals_Recipe ON dbo.CustomerLikedMeals(RecipeId);
END;

IF OBJECT_ID('dbo.DeliveryRoutes','U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryRoutes(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_DeliveryRoutes PRIMARY KEY,
        OutletId uniqueidentifier NOT NULL,
        DriverId uniqueidentifier NOT NULL,
        DeliveryDate datetime2 NOT NULL,
        MealSlot int NOT NULL CONSTRAINT DF_DeliveryRoutes_MealSlot DEFAULT 1,
        Status int NOT NULL CONSTRAINT DF_DeliveryRoutes_Status DEFAULT 0,
        TotalDistanceKm float NOT NULL CONSTRAINT DF_DeliveryRoutes_Distance DEFAULT 0,
        TotalDurationMinutes float NOT NULL CONSTRAINT DF_DeliveryRoutes_Duration DEFAULT 0,
        RoutingSource nvarchar(50) NOT NULL CONSTRAINT DF_DeliveryRoutes_RoutingSource DEFAULT 'OSRM',
        GeometryJson nvarchar(max) NOT NULL CONSTRAINT DF_DeliveryRoutes_Geometry DEFAULT '[]',
        CreatedAtUtc datetime2 NOT NULL,
        UpdatedAtUtc datetime2 NOT NULL
    );
    CREATE INDEX IX_DeliveryRoutes_Outlet_Date ON dbo.DeliveryRoutes(OutletId,DeliveryDate);
    CREATE INDEX IX_DeliveryRoutes_Driver_Date ON dbo.DeliveryRoutes(DriverId,DeliveryDate);
END;
IF OBJECT_ID('dbo.DeliveryRouteStops','U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryRouteStops(
        Id uniqueidentifier NOT NULL CONSTRAINT PK_DeliveryRouteStops PRIMARY KEY,
        RouteId uniqueidentifier NOT NULL,
        StopSequence int NOT NULL,
        MealSlot int NOT NULL CONSTRAINT DF_DeliveryRouteStops_MealSlot DEFAULT 1,
        DeliveryAddressId uniqueidentifier NOT NULL,
        CustomerId uniqueidentifier NOT NULL,
        CustomerName nvarchar(200) NOT NULL,
        Address nvarchar(1000) NOT NULL,
        Latitude float NOT NULL,
        Longitude float NOT NULL,
        DeliveryCount int NOT NULL,
        Status int NOT NULL CONSTRAINT DF_DeliveryRouteStops_Status DEFAULT 0,
        CONSTRAINT FK_DeliveryRouteStops_Routes FOREIGN KEY(RouteId) REFERENCES dbo.DeliveryRoutes(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_DeliveryRouteStops_Route_Sequence ON dbo.DeliveryRouteStops(RouteId,StopSequence);
    CREATE INDEX IX_DeliveryRouteStops_Address ON dbo.DeliveryRouteStops(DeliveryAddressId);
END;
IF COL_LENGTH('dbo.DeliveryRoutes','MealSlot') IS NULL
    ALTER TABLE dbo.DeliveryRoutes ADD MealSlot int NOT NULL CONSTRAINT DF_DeliveryRoutes_MealSlot_Compat DEFAULT 1;
IF COL_LENGTH('dbo.DeliveryRouteStops','MealSlot') IS NULL
    ALTER TABLE dbo.DeliveryRouteStops ADD MealSlot int NOT NULL CONSTRAINT DF_DeliveryRouteStops_MealSlot_Compat DEFAULT 1;
IF COL_LENGTH('dbo.Deliveries','RouteId') IS NULL
    ALTER TABLE dbo.Deliveries ADD RouteId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Deliveries','RouteStopId') IS NULL
    ALTER TABLE dbo.Deliveries ADD RouteStopId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Deliveries','RouteSequence') IS NULL
    ALTER TABLE dbo.Deliveries ADD RouteSequence int NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Deliveries_RouteId' AND object_id=OBJECT_ID('dbo.Deliveries'))
    CREATE INDEX IX_Deliveries_RouteId ON dbo.Deliveries(RouteId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Deliveries_RouteStopId' AND object_id=OBJECT_ID('dbo.Deliveries'))
    CREATE INDEX IX_Deliveries_RouteStopId ON dbo.Deliveries(RouteStopId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.Deliveries') AND referenced_object_id=OBJECT_ID('dbo.DeliveryRoutes'))
    ALTER TABLE dbo.Deliveries ADD CONSTRAINT FK_Deliveries_Routes FOREIGN KEY(RouteId) REFERENCES dbo.DeliveryRoutes(Id) ON DELETE SET NULL;
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name='FK_Deliveries_RouteStops'
      AND parent_object_id=OBJECT_ID('dbo.Deliveries')
      AND referenced_object_id=OBJECT_ID('dbo.DeliveryRouteStops')
      AND delete_referential_action_desc <> 'NO_ACTION'
)
    ALTER TABLE dbo.Deliveries DROP CONSTRAINT FK_Deliveries_RouteStops;
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id=OBJECT_ID('dbo.Deliveries')
      AND referenced_object_id=OBJECT_ID('dbo.DeliveryRouteStops')
)
    ALTER TABLE dbo.Deliveries ADD CONSTRAINT FK_Deliveries_RouteStops FOREIGN KEY(RouteStopId) REFERENCES dbo.DeliveryRouteStops(Id) ON DELETE NO ACTION;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DeliveryRoutes') AND name='TotalDistanceKm' AND system_type_id=59)
    ALTER TABLE dbo.DeliveryRoutes ALTER COLUMN TotalDistanceKm float NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DeliveryRoutes') AND name='TotalDurationMinutes' AND system_type_id=59)
    ALTER TABLE dbo.DeliveryRoutes ALTER COLUMN TotalDurationMinutes float NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DeliveryRouteStops') AND name='Latitude' AND system_type_id=59)
    ALTER TABLE dbo.DeliveryRouteStops ALTER COLUMN Latitude float NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DeliveryRouteStops') AND name='Longitude' AND system_type_id=59)
    ALTER TABLE dbo.DeliveryRouteStops ALTER COLUMN Longitude float NOT NULL;
IF COL_LENGTH('dbo.Subscriptions','DeliveryCity') IS NULL
    ALTER TABLE dbo.Subscriptions ADD DeliveryCity nvarchar(100) NULL;

IF COL_LENGTH('dbo.Outlets','HeroImageUrl') IS NULL
    ALTER TABLE dbo.Outlets ADD HeroImageUrl nvarchar(1000) NULL;
IF COL_LENGTH('dbo.Outlets','HealthHighlights') IS NULL
    ALTER TABLE dbo.Outlets ADD HealthHighlights nvarchar(2000) NULL;
IF COL_LENGTH('dbo.Outlets','Rating') IS NULL
    ALTER TABLE dbo.Outlets ADD Rating float NOT NULL CONSTRAINT DF_Outlets_Rating DEFAULT 4.8;
IF COL_LENGTH('dbo.Outlets','ReviewCount') IS NULL
    ALTER TABLE dbo.Outlets ADD ReviewCount int NOT NULL CONSTRAINT DF_Outlets_ReviewCount DEFAULT 0;
IF COL_LENGTH('dbo.Outlets','About') IS NULL
    ALTER TABLE dbo.Outlets ADD About nvarchar(2000) NULL;

IF COL_LENGTH('dbo.CustomerProfiles','Allergies') IS NOT NULL
    ALTER TABLE dbo.CustomerProfiles ALTER COLUMN Allergies nvarchar(max) NULL;

-- Legacy column retained for older databases; normalized RecipeAllergens/IngredientAllergens are authoritative.
IF COL_LENGTH('dbo.Recipes','FiberGrams') IS NULL
    ALTER TABLE dbo.Recipes ADD FiberGrams int NOT NULL CONSTRAINT DF_Recipes_FiberGrams DEFAULT 0;
IF COL_LENGTH('dbo.Recipes','Allergens') IS NOT NULL
    ALTER TABLE dbo.Recipes ALTER COLUMN Allergens nvarchar(max) NULL;
", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
UPDATE ca
SET City = COALESCE(NULLIF(ca.City, ''), area.City),
    State = COALESCE(NULLIF(ca.State, ''), area.State),
    Pincode = COALESCE(NULLIF(ca.Pincode, ''), area.Pincode),
    Locality = COALESCE(NULLIF(ca.Locality, ''), area.Name)
FROM dbo.CustomerAddresses ca
LEFT JOIN dbo.CityAreas area ON area.Id = ca.CityAreaId
WHERE (ca.City IS NULL OR ca.City = '')
   OR (ca.State IS NULL OR ca.State = '')
   OR (ca.Pincode IS NULL OR ca.Pincode = '')
   OR (ca.Locality IS NULL OR ca.Locality = '');
", cancellationToken);

        // Run data updates in separate SQL batches so SQL Server compiles the UPDATE statements
        // only after any newly-added columns exist. This avoids invalid-column and nested-quote failures.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Subscriptions','DeliveryCity') IS NOT NULL
BEGIN
    UPDATE s
    SET DeliveryCity = COALESCE(NULLIF(s.DeliveryCity, ''), o.City)
    FROM dbo.Subscriptions s
    INNER JOIN dbo.Outlets o ON o.Id = s.OutletId
    WHERE s.DeliveryCity IS NULL OR s.DeliveryCity = '';
END;
", cancellationToken);

        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Outlets','HeroImageUrl') IS NOT NULL
   AND COL_LENGTH('dbo.Outlets','HealthHighlights') IS NOT NULL
BEGIN
    UPDATE dbo.Outlets
    SET HeroImageUrl = COALESCE(NULLIF(HeroImageUrl, ''), 'https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=1200&q=85'),
        HealthHighlights = COALESCE(NULLIF(HealthHighlights, ''), 'Grilled,Cold Pressed Oil,High Protein,Exotic Bowls,Fresh Ingredients')
    WHERE Slug = 'fitfood';

    UPDATE dbo.Outlets
    SET HeroImageUrl = COALESCE(NULLIF(HeroImageUrl, ''), 'https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=1200&q=85'),
        HealthHighlights = COALESCE(NULLIF(HealthHighlights, ''), 'Fresh Ingredients,Balanced Nutrition,Vegetarian Friendly,High Protein')
    WHERE Slug = 'abc';
END;
", cancellationToken);

        // Existing demo databases can contain NULLs in columns that are now represented by
        // non-nullable C# strings. EF Core materializes those columns with GetString(), which
        // results in SqlNullValueException. Normalize legacy NULLs before any repository query runs.
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Users','Email') IS NOT NULL UPDATE dbo.Users SET Email = COALESCE(Email,'');
IF COL_LENGTH('dbo.Users','PasswordHash') IS NOT NULL UPDATE dbo.Users SET PasswordHash = COALESCE(PasswordHash,'');
IF COL_LENGTH('dbo.Users','FirstName') IS NOT NULL UPDATE dbo.Users SET FirstName = COALESCE(FirstName,'');
IF COL_LENGTH('dbo.Users','LastName') IS NOT NULL UPDATE dbo.Users SET LastName = COALESCE(LastName,'');

IF COL_LENGTH('dbo.Outlets','Name') IS NOT NULL UPDATE dbo.Outlets SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.Outlets','Slug') IS NOT NULL UPDATE dbo.Outlets SET Slug = COALESCE(Slug,'');
IF COL_LENGTH('dbo.Outlets','Subdomain') IS NOT NULL UPDATE dbo.Outlets SET Subdomain = COALESCE(Subdomain,'');
IF COL_LENGTH('dbo.Outlets','City') IS NOT NULL UPDATE dbo.Outlets SET City = COALESCE(City,'');
IF COL_LENGTH('dbo.Outlets','State') IS NOT NULL UPDATE dbo.Outlets SET State = COALESCE(State,'');
IF COL_LENGTH('dbo.Outlets','Pincode') IS NOT NULL UPDATE dbo.Outlets SET Pincode = COALESCE(Pincode,'');
IF COL_LENGTH('dbo.Outlets','LogoUrl') IS NOT NULL UPDATE dbo.Outlets SET LogoUrl = COALESCE(LogoUrl,'');
IF COL_LENGTH('dbo.Outlets','HeroImageUrl') IS NOT NULL UPDATE dbo.Outlets SET HeroImageUrl = COALESCE(HeroImageUrl,'');
IF COL_LENGTH('dbo.Outlets','HealthHighlights') IS NOT NULL UPDATE dbo.Outlets SET HealthHighlights = COALESCE(HealthHighlights,'');
IF COL_LENGTH('dbo.Outlets','PrimaryColor') IS NOT NULL UPDATE dbo.Outlets SET PrimaryColor = COALESCE(PrimaryColor,'#14532d');
IF COL_LENGTH('dbo.Outlets','About') IS NOT NULL UPDATE dbo.Outlets SET About = COALESCE(About,'');

IF COL_LENGTH('dbo.CustomerProfiles','Goal') IS NOT NULL UPDATE dbo.CustomerProfiles SET Goal = COALESCE(Goal,'WeightLoss');
IF COL_LENGTH('dbo.CustomerProfiles','ActivityLevel') IS NOT NULL UPDATE dbo.CustomerProfiles SET ActivityLevel = COALESCE(ActivityLevel,'Moderate');
IF COL_LENGTH('dbo.CustomerProfiles','Diet') IS NOT NULL UPDATE dbo.CustomerProfiles SET Diet = COALESCE(Diet,'');

IF COL_LENGTH('dbo.CustomerAddresses','City') IS NOT NULL UPDATE dbo.CustomerAddresses SET City = COALESCE(City,'');
IF COL_LENGTH('dbo.CustomerAddresses','State') IS NOT NULL UPDATE dbo.CustomerAddresses SET State = COALESCE(State,'');
IF COL_LENGTH('dbo.CustomerAddresses','Pincode') IS NOT NULL UPDATE dbo.CustomerAddresses SET Pincode = COALESCE(Pincode,'');
IF COL_LENGTH('dbo.CustomerAddresses','Locality') IS NOT NULL UPDATE dbo.CustomerAddresses SET Locality = COALESCE(Locality,'');
IF COL_LENGTH('dbo.CustomerAddresses','Label') IS NOT NULL UPDATE dbo.CustomerAddresses SET Label = COALESCE(Label,'');
IF COL_LENGTH('dbo.CustomerAddresses','AddressLine1') IS NOT NULL UPDATE dbo.CustomerAddresses SET AddressLine1 = COALESCE(AddressLine1,'');
IF COL_LENGTH('dbo.CustomerAddresses','AddressLine2') IS NOT NULL UPDATE dbo.CustomerAddresses SET AddressLine2 = COALESCE(AddressLine2,'');
IF COL_LENGTH('dbo.CustomerAddresses','ContactName') IS NOT NULL UPDATE dbo.CustomerAddresses SET ContactName = COALESCE(ContactName,'');
IF COL_LENGTH('dbo.CustomerAddresses','ContactPhone') IS NOT NULL UPDATE dbo.CustomerAddresses SET ContactPhone = COALESCE(ContactPhone,'');

IF COL_LENGTH('dbo.Subscriptions','DeliveryCity') IS NOT NULL UPDATE dbo.Subscriptions SET DeliveryCity = COALESCE(DeliveryCity,'');
IF COL_LENGTH('dbo.Subscriptions','PlanName') IS NOT NULL UPDATE dbo.Subscriptions SET PlanName = COALESCE(PlanName,'');
IF COL_LENGTH('dbo.Subscriptions','Frequency') IS NOT NULL UPDATE dbo.Subscriptions SET Frequency = COALESCE(Frequency,'Weekly');
IF COL_LENGTH('dbo.Subscriptions','DiscountCode') IS NOT NULL UPDATE dbo.Subscriptions SET DiscountCode = COALESCE(DiscountCode,'');

IF COL_LENGTH('dbo.Recipes','Name') IS NOT NULL UPDATE dbo.Recipes SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.Recipes','Description') IS NOT NULL UPDATE dbo.Recipes SET Description = COALESCE(Description,'');
IF COL_LENGTH('dbo.Recipes','ImageUrl') IS NOT NULL UPDATE dbo.Recipes SET ImageUrl = COALESCE(ImageUrl,'');
IF COL_LENGTH('dbo.Recipes','Tags') IS NOT NULL UPDATE dbo.Recipes SET Tags = COALESCE(Tags,'');
IF COL_LENGTH('dbo.Recipes','Allergens') IS NOT NULL UPDATE dbo.Recipes SET Allergens = COALESCE(Allergens,'');

IF COL_LENGTH('dbo.Ingredients','Name') IS NOT NULL UPDATE dbo.Ingredients SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.Ingredients','DefaultUnit') IS NOT NULL UPDATE dbo.Ingredients SET DefaultUnit = COALESCE(DefaultUnit,'g');
IF COL_LENGTH('dbo.Allergens','Name') IS NOT NULL UPDATE dbo.Allergens SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.RecipeIngredients','Unit') IS NOT NULL UPDATE dbo.RecipeIngredients SET Unit = COALESCE(Unit,'g');
IF COL_LENGTH('dbo.MealPlans','Name') IS NOT NULL UPDATE dbo.MealPlans SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.MealPlans','Frequency') IS NOT NULL UPDATE dbo.MealPlans SET Frequency = COALESCE(Frequency,'Weekly');
IF COL_LENGTH('dbo.MealPlans','Currency') IS NOT NULL UPDATE dbo.MealPlans SET Currency = COALESCE(Currency,'INR');
IF COL_LENGTH('dbo.MealPlans','Description') IS NOT NULL UPDATE dbo.MealPlans SET Description = COALESCE(Description,'');
IF COL_LENGTH('dbo.SaaSPlans','Name') IS NOT NULL UPDATE dbo.SaaSPlans SET Name = COALESCE(Name,'');
IF COL_LENGTH('dbo.SaaSPlans','Description') IS NOT NULL UPDATE dbo.SaaSPlans SET Description = COALESCE(Description,'');
IF COL_LENGTH('dbo.OutletSubscriptions','BillingCycle') IS NOT NULL UPDATE dbo.OutletSubscriptions SET BillingCycle = COALESCE(BillingCycle,'Monthly');
IF COL_LENGTH('dbo.OutletSubscriptions','Status') IS NOT NULL UPDATE dbo.OutletSubscriptions SET Status = COALESCE(Status,'Active');

IF COL_LENGTH('dbo.Orders','Address') IS NOT NULL UPDATE dbo.Orders SET Address = COALESCE(Address,'');
IF COL_LENGTH('dbo.Deliveries','CustomerName') IS NOT NULL UPDATE dbo.Deliveries SET CustomerName = COALESCE(CustomerName,'');
IF COL_LENGTH('dbo.Deliveries','Address') IS NOT NULL UPDATE dbo.Deliveries SET Address = COALESCE(Address,'');
IF COL_LENGTH('dbo.DeliveryRouteStops','CustomerName') IS NOT NULL UPDATE dbo.DeliveryRouteStops SET CustomerName = COALESCE(CustomerName,'');
IF COL_LENGTH('dbo.DeliveryRouteStops','Address') IS NOT NULL UPDATE dbo.DeliveryRouteStops SET Address = COALESCE(Address,'');
IF COL_LENGTH('dbo.DeliveryRoutes','RoutingSource') IS NOT NULL UPDATE dbo.DeliveryRoutes SET RoutingSource = COALESCE(RoutingSource,'OSRM');
IF COL_LENGTH('dbo.DeliveryRoutes','GeometryJson') IS NOT NULL UPDATE dbo.DeliveryRoutes SET GeometryJson = COALESCE(GeometryJson,'[]');
IF COL_LENGTH('dbo.MealSelectionHistories','Action') IS NOT NULL UPDATE dbo.MealSelectionHistories SET Action = COALESCE(Action,'');
IF COL_LENGTH('dbo.MealSelectionHistories','Reason') IS NOT NULL UPDATE dbo.MealSelectionHistories SET Reason = COALESCE(Reason,'');
IF COL_LENGTH('dbo.PaymentTransactions','Provider') IS NOT NULL UPDATE dbo.PaymentTransactions SET Provider = COALESCE(Provider,'Mock');
IF COL_LENGTH('dbo.PaymentTransactions','ProviderPaymentId') IS NOT NULL UPDATE dbo.PaymentTransactions SET ProviderPaymentId = COALESCE(ProviderPaymentId,'');
IF COL_LENGTH('dbo.PaymentTransactions','IdempotencyKey') IS NOT NULL UPDATE dbo.PaymentTransactions SET IdempotencyKey = COALESCE(IdempotencyKey,'');
IF COL_LENGTH('dbo.PaymentTransactions','Currency') IS NOT NULL UPDATE dbo.PaymentTransactions SET Currency = COALESCE(Currency,'INR');
IF COL_LENGTH('dbo.PaymentTransactions','Status') IS NOT NULL UPDATE dbo.PaymentTransactions SET Status = COALESCE(Status,'Pending');
IF COL_LENGTH('dbo.DiscountCodes','Code') IS NOT NULL UPDATE dbo.DiscountCodes SET Code = COALESCE(Code,'');
", cancellationToken);

        // Keep the old text columns harmless for older databases; normalized values are now authoritative.
        await DatabaseSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<HealthApp.Application.Abstractions.IPasswordService>(), cancellationToken);
    }
}
