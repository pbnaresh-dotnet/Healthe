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
        // Development/demo compatibility for databases created before Recipe.Ingredients was added.
        await db.Database.ExecuteSqlRawAsync(
            "IF COL_LENGTH('dbo.Recipes','Ingredients') IS NULL ALTER TABLE dbo.Recipes ADD Ingredients nvarchar(3000) NOT NULL CONSTRAINT DF_Recipes_Ingredients DEFAULT N'';",
            cancellationToken);
        await DatabaseSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<HealthApp.Application.Abstractions.IPasswordService>(), cancellationToken);
    }
}
