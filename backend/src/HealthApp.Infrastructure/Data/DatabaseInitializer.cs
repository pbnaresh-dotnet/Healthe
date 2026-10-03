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
", cancellationToken);
        // Keep the old text columns harmless for older databases; normalized values are now authoritative.
        await DatabaseSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<HealthApp.Application.Abstractions.IPasswordService>(), cancellationToken);
    }
}
