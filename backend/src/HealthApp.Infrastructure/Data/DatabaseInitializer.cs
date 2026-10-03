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
IF COL_LENGTH('dbo.CustomerProfiles','Allergies') IS NOT NULL
    ALTER TABLE dbo.CustomerProfiles ALTER COLUMN Allergies nvarchar(max) NULL;
", cancellationToken);
        // Keep the old text columns harmless for older databases; normalized values are now authoritative.
        await DatabaseSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<HealthApp.Application.Abstractions.IPasswordService>(), cancellationToken);
    }
}
