# HealthApp Backend

Clean architecture:

```text
Api
  -> Application
      -> Domain
  -> Infrastructure
```

EF Core lives in Infrastructure. Application services use repository abstractions and Unit of Work.

The demo startup uses `Database.EnsureCreatedAsync()` so the checked-in package can initialize SQL Server without a generated migration bundle. For production schema evolution, generate EF Core migrations from `HealthAppDbContext` and change the initializer to `MigrateAsync()`.

Important persistence components:

- `Infrastructure/Data/HealthAppDbContext.cs`
- `Infrastructure/Data/HealthAppModelBuilder.cs`
- `Infrastructure/Repositories/EfRepositories.cs`
- `Infrastructure/Data/EfUnitOfWork.cs`
- `Infrastructure/DeliveryCalculator.cs`
