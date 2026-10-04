using HealthApp.Application.Abstractions;
using HealthApp.Application.Events;
using HealthApp.Application.Orchestration;
using HealthApp.Application.Services;
using HealthApp.Application.Strategies;
using HealthApp.Domain.Events;
using HealthApp.Infrastructure.Authentication;
using HealthApp.Infrastructure.Data;
using HealthApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HealthApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<HealthAppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(HealthAppDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            }));

        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.Configure<GeocodingOptions>(config.GetSection("Geocoding"));

        var storageProvider = (config["Storage:Provider"] ?? "Local").Trim().ToLowerInvariant();
        switch (storageProvider)
        {
            case "azureblob":
                services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
                break;
            case "local":
                services.AddSingleton<IFileStorage, LocalFileStorage>();
                break;
            default:
                throw new InvalidOperationException($"Unsupported Storage:Provider '{storageProvider}'.");
        }

        var geocodingProvider = (config["Geocoding:Provider"] ?? "Nominatim").Trim().ToLowerInvariant();
        if (geocodingProvider == "nominatim")
        {
            services.AddHttpClient<IGeocodingService, NominatimGeocodingService>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeocodingOptions>>().Value;
                var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://nominatim.openstreetmap.org" : options.BaseUrl.TrimEnd('/');
                client.BaseAddress = new Uri(baseUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(string.IsNullOrWhiteSpace(options.UserAgent) ? "HealthApp/1.0" : options.UserAgent);
                if (!string.IsNullOrWhiteSpace(options.Referer))
                    client.DefaultRequestHeaders.Referrer = new Uri(options.Referer);
            });
        }
        else
        {
            throw new InvalidOperationException($"Unsupported Geocoding:Provider '{geocodingProvider}'.");
        }
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOutletRepository, OutletRepository>();
        services.AddScoped<ISaaSPlanRepository, SaaSPlanRepository>();
        services.AddScoped<IOutletSubscriptionRepository, OutletSubscriptionRepository>();
        services.AddScoped<IPlatformTransactionRepository, PlatformTransactionRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IAllergenRepository, AllergenRepository>();
        services.AddScoped<ICustomerAllergyRepository, CustomerAllergyRepository>();
        services.AddScoped<IMealPlanRepository, MealPlanRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IOutletMenuRepository, OutletMenuRepository>();
        services.AddScoped<ICustomerCreditRepository, CustomerCreditRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISubscriptionMealSelectionRepository, SubscriptionMealSelectionRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IDeliveryRouteRepository, DeliveryRouteRepository>();
        services.AddScoped<ICustomerProfileRepository, CustomerProfileRepository>();
        services.AddScoped<ICityAreaRepository, CityAreaRepository>();
        services.AddScoped<IOutletDeliveryAreaRepository, OutletDeliveryAreaRepository>();
        services.AddScoped<IDeliveryPricingRepository, DeliveryPricingRepository>();
        services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
        services.AddScoped<ISubscriptionDiscountTierRepository, SubscriptionDiscountTierRepository>();
        services.AddScoped<IMealSelectionHistoryRepository, MealSelectionHistoryRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IDiscountCodeRepository, DiscountCodeRepository>();
        services.AddScoped<IOrderFinancialRepository, OrderFinancialRepository>();
        services.AddScoped<IDeliveryCalculator, DeliveryCalculator>();
        services.AddHttpClient<IRouteMatrixService, OsrmRouteMatrixService>((_, client) =>
        {
            client.BaseAddress = new Uri(config["Routing:OsrmBaseUrl"] ?? "https://router.project-osrm.org");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddHttpClient<IRouteOptimizationService, OsrmRouteOptimizationService>((_, client) =>
        {
            client.BaseAddress = new Uri(config["Routing:OsrmBaseUrl"] ?? "https://router.project-osrm.org");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddScoped<IMultiDriverRoutePlanningService, OrToolsMultiDriverRoutePlanningService>();

        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<MealSkippedEvent>, LateSkipFeeRevenueHandler>();

        services.AddSingleton<IPackageDiscountStrategy, DurationAndVolumeDiscountStrategy>();
        services.AddSingleton<IMealPriceStrategy, RecipeMealPriceStrategy>();
        services.AddSingleton<ILateSkipFeePolicy, MidnightLateSkipFeePolicy>();
        services.AddSingleton<IPlatformServiceFeeStrategy, ConfigurablePlatformServiceFeeStrategy>();
        services.AddSingleton<ITaxStrategy, ConfigurableTaxStrategy>();
        services.AddSingleton<IDeliveryModeStrategy, IndividualMealDeliveryStrategy>();
        services.AddSingleton<IDeliveryModeStrategy, OneDeliveryPerDayStrategy>();
        services.AddSingleton<IDeliveryModeStrategyFactory, DeliveryModeStrategyFactory>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMarketplaceService, MarketplaceService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IAllergySafetyService, AllergySafetyService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOutletService, OutletService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ICustomerProfileService, CustomerProfileService>();
        services.AddScoped<ICustomerAddressService, CustomerAddressService>();
        services.AddScoped<IOutletDeliveryService, OutletDeliveryService>();
        services.AddScoped<IDiscountConfigurationService, DiscountConfigurationService>();
        services.AddScoped<ICityAreaAdminService, CityAreaAdminService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IDeliveryLabelService, DeliveryLabelService>();
        services.AddScoped<IDeliveryRouteService, DeliveryRouteService>();
        services.AddScoped<IOutletDiscountCodeService, OutletDiscountCodeService>();
        return services;
    }
}
