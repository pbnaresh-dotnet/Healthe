namespace HealthApp.Shared.DTOs;

public record OutletGroupDto(Guid Id, string Name, string Description, bool IsActive, int SortOrder, int OutletCount);

public record CreateOutletGroupRequest(string Name, string Description = "", bool IsActive = true, int SortOrder = 0);

public record UpdateOutletGroupRequest(string Name, string Description = "", bool IsActive = true, int SortOrder = 0);

public record AssignOutletGroupRequest(Guid? OutletGroupId);

public record AdminOutlet360Dto(
    OutletDto Outlet,
    OutletGroupDto? Group,
    int TotalUsers,
    int CustomerUsers,
    int StaffUsers,
    int ActiveCustomers,
    int TotalSubscriptions,
    int ActiveSubscriptions,
    int PendingSubscriptions,
    int CancelledSubscriptions,
    int Orders30Days,
    int Deliveries30Days,
    int DeliveriesToday,
    decimal Revenue30Days,
    decimal RestaurantGst30Days,
    decimal PlatformFee30Days,
    decimal PlatformRevenue30Days,
    OutletBillingDto? Billing,
    OutletTaxSettingsDto TaxSettings,
    bool LegalPoliciesPublished,
    string LegalVersion,
    DateTime? LegalEffectiveDateUtc,
    IReadOnlyList<OutletDomainDto> Domains,
    IReadOnlyList<UserDto> Staff,
    IReadOnlyList<SubscriptionDto> RecentSubscriptions,
    IReadOnlyList<OrderDto> RecentOrders,
    IReadOnlyList<DeliveryDto> RecentDeliveries);
