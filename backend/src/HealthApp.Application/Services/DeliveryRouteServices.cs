using System.Text.Json;
using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Application.Services;

public sealed class DeliveryRouteService(
    ICurrentUser current,
    IOutletRepository outlets,
    IUserRepository users,
    IPasswordService passwords,
    IDeliveryRepository deliveries,
    ICustomerAddressRepository addresses,
    IDeliveryRouteRepository routes,
    IRouteOptimizationService optimizer,
    IRouteMatrixService matrix,
    IMultiDriverRoutePlanningService planner) : IDeliveryRouteService
{
    public async Task<IReadOnlyList<DriverDto>> GetDriversAsync()
    {
        if (current.OutletId is not Guid outletId) return [];
        return (await users.GetAllAsync())
            .Where(x => x.OutletId == outletId && x.Role == UserRole.Driver && x.IsActive)
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Select(MapDriver)
            .ToList();
    }

    public async Task<DriverDto?> CreateDriverAsync(CreateDriverRequest request)
    {
        if (current.OutletId is not Guid outletId) return null;
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new ArgumentException("Driver first and last name are required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Driver email is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ArgumentException("Driver password must be at least 6 characters.");
        if (await users.FindByEmailAsync(request.Email.Trim(), outletId) is not null)
            throw new InvalidOperationException("Email is already registered for this outlet.");

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.Driver,
            OutletId = outletId,
            PasswordHash = passwords.Hash(request.Password),
            IsActive = true
        };

        await users.AddAsync(driver);
        return MapDriver(driver);
    }

    public async Task<DeliveryRoutePlanDto> GetPlanAsync(DateTime date, int mealSlotValue = (int)MealSlot.Afternoon)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("Outlet context is required.");

        if (!Enum.IsDefined(typeof(MealSlot), mealSlotValue))
            throw new ArgumentException("Invalid meal slot.");

        var mealSlot = (MealSlot)mealSlotValue;
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        var eligible = await GetEligibleDeliveriesAsync(outletId, date, mealSlot);
        var pointGroups = await BuildPointGroupsAsync(eligible);
        var existingRoutes = await routes.GetByOutletAndDateAsync(outletId, date.Date, mealSlot);
        var routeDtos = await MapRoutesAsync(existingRoutes, eligible);

        var assignedPointKeys = existingRoutes
            .SelectMany(x => x.Stops)
            .Select(x => x.DeliveryAddressId)
            .ToHashSet();

        var plannedDistanceKm = existingRoutes.Sum(x => x.TotalDistanceKm);
        var plannedDurationMinutes = existingRoutes.Sum(x => x.TotalDurationMinutes);
        var optimizationSources = existingRoutes.Select(x => x.RoutingSource).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var optimizationSource = optimizationSources.Count == 0 ? "Not calculated" : string.Join(" + ", optimizationSources);

        return new DeliveryRoutePlanDto(
            date.Date,
            outlet.Name,
            outlet.Latitude,
            outlet.Longitude,
            pointGroups.Count,
            eligible.Count,
            Math.Max(0, pointGroups.Count - assignedPointKeys.Count),
            Math.Round(plannedDistanceKm, 2),
            Math.Round(plannedDurationMinutes, 1),
            optimizationSource,
            pointGroups.Select(x => new DeliveryMapPointDto(
                x.Representative.Id,
                x.Representative.CustomerId,
                x.Representative.CustomerName,
                x.Address.Id,
                x.Representative.Address,
                x.Address.Latitude,
                x.Address.Longitude,
                x.Deliveries.Count,
                x.Representative.MealSlot.ToString(),
                x.GroupStatus,
                x.Representative.RouteId,
                x.Representative.RouteSequence)).ToList(),
            routeDtos);
    }

    public async Task<DeliveryRoutePlanDto> PlanRoutesAsync(PlanDeliveryRoutesRequest request)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("Outlet context is required.");

        if (!Enum.IsDefined(typeof(MealSlot), request.MealSlot))
            throw new ArgumentException("Invalid meal slot.");

        var mealSlot = (MealSlot)request.MealSlot;
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        var selectedDriverIds = request.DriverIds?.Distinct().ToList() ?? [];

        var selectedDrivers = (await users.GetAllAsync())
            .Where(x => x.OutletId == outletId &&
                        x.Role == UserRole.Driver &&
                        x.IsActive &&
                        selectedDriverIds.Contains(x.Id))
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToList();

        if (selectedDrivers.Count == 0)
            throw new ArgumentException("Select at least one active in-house driver.");
        if (selectedDrivers.Count != selectedDriverIds.Count)
            throw new ArgumentException("One or more selected drivers are invalid for this outlet.");

        var date = request.Date.Date;
        var eligible = await GetEligibleDeliveriesAsync(outletId, date, mealSlot);

        await routes.DeleteByOutletAndDateAsync(outletId, date, mealSlot);

        if (eligible.Count == 0)
            return await GetPlanAsync(date, request.MealSlot);

        var pointGroups = await BuildPointGroupsAsync(eligible);
        if (pointGroups.Count == 0)
            return await GetPlanAsync(date, request.MealSlot);

        var matrixPoints = new List<RouteOptimizationStop> {
            new(Guid.Empty, outlet.Latitude, outlet.Longitude)
        };
        matrixPoints.AddRange(pointGroups.Select(x =>
            new RouteOptimizationStop(x.Address.Id, x.Address.Latitude, x.Address.Longitude)));

        var byAddressId = pointGroups.ToDictionary(x => x.Address.Id);

        MultiDriverRoutePlan multiPlan;
        var globalRoutingSource = "OR-Tools + OSRM matrix";

        if (request.ManualAssignments is { Count: > 0 })
        {
            var selectedDriverSet = selectedDrivers.Select(x => x.Id).ToHashSet();
            var seenAddressIds = new HashSet<Guid>();
            var manualRoutes = new List<DriverRouteAssignment>();

            foreach (var assignment in request.ManualAssignments)
            {
                if (!selectedDriverSet.Contains(assignment.DriverId))
                    throw new ArgumentException("Every manual route assignment must use a selected active driver.");

                var addressIds = (assignment.AddressIds ?? [])
                    .Distinct()
                    .Where(byAddressId.ContainsKey)
                    .ToList();

                foreach (var addressId in addressIds)
                {
                    if (!seenAddressIds.Add(addressId))
                        throw new ArgumentException("A delivery stop cannot be assigned to more than one driver.");
                }

                if (addressIds.Count > 0)
                    manualRoutes.Add(new DriverRouteAssignment(assignment.DriverId, addressIds));
            }

            var missing = byAddressId.Keys.Where(id => !seenAddressIds.Contains(id)).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"Assign all delivery stops before saving manual routes. {missing.Count} stop(s) remain unassigned.");

            multiPlan = new MultiDriverRoutePlan(manualRoutes);
            globalRoutingSource = "Manual driver assignment";
        }
        else
        {
            try
            {
                var matrixResult = await matrix.BuildAsync(matrixPoints);
                multiPlan = await planner.OptimizeAsync(
                    selectedDrivers.Select(x => x.Id).ToList(),
                    matrixPoints,
                    matrixResult);
            }
            catch
            {
                multiPlan = BuildFallbackMultiDriverPlan(outlet, pointGroups, selectedDrivers);
                globalRoutingSource = "Geographic fallback";
            }
        }

        foreach (var assignment in multiPlan.Routes)
        {
            var orderedGroups = assignment.StopIds
                .Where(byAddressId.ContainsKey)
                .Select(id => byAddressId[id])
                .ToList();

            if (orderedGroups.Count == 0)
                continue;

            RouteOptimizationResult optimized;
            var routeSource = globalRoutingSource + " + OSRM road route";

            try
            {
                optimized = await optimizer.RouteInOrderAsync(
                    outlet.Latitude,
                    outlet.Longitude,
                    orderedGroups.Select(x => new RouteOptimizationStop(
                        x.Address.Id,
                        x.Address.Latitude,
                        x.Address.Longitude)).ToList());
            }
            catch
            {
                optimized = BuildFallbackRoute(outlet, orderedGroups);
                routeSource = globalRoutingSource + " + straight-line fallback";
            }

            var routeId = Guid.NewGuid();
            var route = new DeliveryRoute
            {
                Id = routeId,
                OutletId = outletId,
                DriverId = assignment.DriverId,
                DeliveryDate = date,
                MealSlot = mealSlot,
                Status = RouteStatus.Planned,
                TotalDistanceKm = Math.Round(optimized.DistanceKm, 2),
                TotalDurationMinutes = Math.Round(optimized.DurationMinutes, 1),
                RoutingSource = routeSource,
                GeometryJson = JsonSerializer.Serialize(optimized.Geometry),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var orderedIds = optimized.OrderedStopIds.Count == orderedGroups.Count
                ? optimized.OrderedStopIds
                : orderedGroups.Select(x => x.Address.Id).ToList();

            var sequence = 1;
            var deliveryLinks = new List<(Delivery Delivery, Guid StopId, int Sequence)>();

            foreach (var addressId in orderedIds)
            {
                if (!byAddressId.TryGetValue(addressId, out var point))
                    continue;

                var stopId = Guid.NewGuid();
                route.Stops.Add(new DeliveryRouteStop
                {
                    Id = stopId,
                    RouteId = routeId,
                    StopSequence = sequence,
                    MealSlot = mealSlot,
                    DeliveryAddressId = point.Address.Id,
                    CustomerId = point.Representative.CustomerId,
                    CustomerName = point.Representative.CustomerName,
                    Address = point.Representative.Address,
                    Latitude = point.Address.Latitude,
                    Longitude = point.Address.Longitude,
                    DeliveryCount = point.Deliveries.Count,
                    Status = ParseDeliveryStatus(point.GroupStatus)
                });

                foreach (var delivery in point.Deliveries)
                    deliveryLinks.Add((delivery, stopId, sequence));

                sequence++;
            }

            await routes.AddAsync(route);

            foreach (var link in deliveryLinks)
            {
                link.Delivery.RouteId = routeId;
                link.Delivery.RouteStopId = link.StopId;
                link.Delivery.RouteSequence = link.Sequence;
                await deliveries.UpdateAsync(link.Delivery);
            }
        }

        return await GetPlanAsync(date, request.MealSlot);
    }

    public async Task<DeliveryRoutePlanDto> DispatchRouteAsync(Guid routeId)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("Outlet context is required.");

        var route = await routes.GetAsync(routeId)
            ?? throw new KeyNotFoundException("Delivery route not found.");

        if (route.OutletId != outletId)
            throw new UnauthorizedAccessException("Delivery route does not belong to this outlet.");

        if (route.Status != RouteStatus.Planned)
            throw new InvalidOperationException($"Only planned routes can be dispatched. Current status: {route.Status}.");

        if (route.DriverId == Guid.Empty)
            throw new InvalidOperationException("A driver must be assigned before dispatching the route.");

        if (route.Stops.Count == 0)
            throw new InvalidOperationException("A route must contain at least one delivery stop before dispatching.");

        route.Status = RouteStatus.Dispatched;
        route.UpdatedAtUtc = DateTime.UtcNow;
        await routes.UpdateAsync(route);

        return await GetPlanAsync(route.DeliveryDate.Date, (int)route.MealSlot);
    }

    private async Task<List<Delivery>> GetEligibleDeliveriesAsync(Guid outletId, DateTime date, MealSlot mealSlot)
    {
        var rows = await deliveries.GetByOutletAsync(outletId);
        return rows
            .Where(x => x.ScheduledDate.Date == date.Date &&
                        x.MealSlot == mealSlot &&
                        (x.Status == DeliveryStatus.Scheduled || x.Status == DeliveryStatus.Preparing))
            .OrderBy(x => x.ScheduledDate)
            .ToList();
    }

    private async Task<List<DeliveryPointGroup>> BuildPointGroupsAsync(IReadOnlyList<Delivery> rows)
    {
        var result = new List<DeliveryPointGroup>();

        foreach (var group in rows
            .Where(x => x.DeliveryAddressId.HasValue)
            .GroupBy(x => new { AddressId = x.DeliveryAddressId!.Value, x.MealSlot }))
        {
            var representative = group.First();
            var address = await addresses.GetAsync(representative.CustomerId, group.Key.AddressId);
            if (address is null || !IsValidCoordinate(address.Latitude, address.Longitude)) continue;

            var deliveriesInGroup = group.ToList();
            result.Add(new DeliveryPointGroup(
                group.Key.AddressId,
                representative,
                address,
                deliveriesInGroup,
                deliveriesInGroup.Select(x => x.Status).Distinct().Count() == 1
                    ? deliveriesInGroup[0].Status.ToString()
                    : "Scheduled"));
        }

        return result;
    }

    private async Task<IReadOnlyList<DeliveryRouteDto>> MapRoutesAsync(
        IReadOnlyList<DeliveryRoute> routeRows,
        IReadOnlyList<Delivery> eligible)
    {
        var driverMap = (await users.GetAllAsync()).ToDictionary(x => x.Id);

        return routeRows.Select(route =>
        {
            driverMap.TryGetValue(route.DriverId, out var driver);
            IReadOnlyList<IReadOnlyList<double>> geometry;
            try
            {
                geometry = JsonSerializer.Deserialize<List<double[]>>(route.GeometryJson)
                    ?.Select(x => (IReadOnlyList<double>)x).ToList()
                    ?? [];
            }
            catch
            {
                geometry = [];
            }

            var stops = route.Stops
                .OrderBy(x => x.StopSequence)
                .Select(stop =>
                {
                    var linked = eligible.Where(x => x.RouteStopId == stop.Id).ToList();
                    return new DeliveryRouteStopDto(
                        stop.Id,
                        stop.StopSequence,
                        stop.MealSlot.ToString(),
                        stop.DeliveryAddressId,
                        stop.CustomerId,
                        stop.CustomerName,
                        stop.Address,
                        stop.Latitude,
                        stop.Longitude,
                        Math.Max(stop.DeliveryCount, linked.Count),
                        stop.Status.ToString(),
                        linked.Select(x => x.Id).ToList());
                })
                .ToList();

            return new DeliveryRouteDto(
                route.Id,
                route.DriverId,
                driver is null ? "Unassigned driver" : $"{driver.FirstName} {driver.LastName}".Trim(),
                route.DeliveryDate,
                route.MealSlot.ToString(),
                GetDeliveryWindow(route.MealSlot),
                route.Status.ToString(),
                route.TotalDistanceKm,
                route.TotalDurationMinutes,
                route.RoutingSource,
                stops,
                geometry);
        }).ToList();
    }

    private static string GetDeliveryWindow(MealSlot slot)
        => slot switch
        {
            MealSlot.Morning => "07:00–09:00",
            MealSlot.Afternoon => "12:00–14:00",
            MealSlot.Evening => "17:00–19:00",
            MealSlot.Night => "20:00–22:00",
            _ => "Scheduled"
        };

    private static DriverDto MapDriver(User x) => new(x.Id, $"{x.FirstName} {x.LastName}".Trim(), x.Email, x.IsActive);

    private static DeliveryStatus ParseDeliveryStatus(string value)
        => Enum.TryParse<DeliveryStatus>(value, true, out var result) ? result : DeliveryStatus.Scheduled;

    private static MultiDriverRoutePlan BuildFallbackMultiDriverPlan(
        Outlet outlet,
        IReadOnlyList<DeliveryPointGroup> points,
        IReadOnlyList<User> drivers)
    {
        var usableDriverCount = Math.Min(drivers.Count, points.Count);
        if (usableDriverCount == 0)
            return new MultiDriverRoutePlan([]);

        var angular = points
            .OrderBy(x => Math.Atan2(
                x.Address.Latitude - outlet.Latitude,
                x.Address.Longitude - outlet.Longitude))
            .ThenBy(x => Haversine(
                outlet.Latitude,
                outlet.Longitude,
                x.Address.Latitude,
                x.Address.Longitude))
            .ToList();

        var chunkSize = (int)Math.Ceiling(angular.Count / (double)usableDriverCount);
        var assignments = new List<DriverRouteAssignment>();

        for (var i = 0; i < usableDriverCount; i++)
        {
            var stopIds = angular
                .Skip(i * chunkSize)
                .Take(chunkSize)
                .Select(x => x.Address.Id)
                .ToList();

            if (stopIds.Count > 0)
                assignments.Add(new DriverRouteAssignment(drivers[i].Id, stopIds));
        }

        return new MultiDriverRoutePlan(assignments);
    }

    private static RouteOptimizationResult BuildFallbackRoute(Outlet outlet, IReadOnlyList<DeliveryPointGroup> points)
    {
        var remaining = points.ToList();
        var ordered = new List<Guid>();
        var lat = outlet.Latitude;
        var lon = outlet.Longitude;
        var geometry = new List<IReadOnlyList<double>> { new[] { lon, lat } };
        var distance = 0d;

        while (remaining.Count > 0)
        {
            var next = remaining.OrderBy(x => Haversine(lat, lon, x.Address.Latitude, x.Address.Longitude)).First();
            remaining.Remove(next);
            distance += Haversine(lat, lon, next.Address.Latitude, next.Address.Longitude);
            lat = next.Address.Latitude;
            lon = next.Address.Longitude;
            ordered.Add(next.Address.Id);
            geometry.Add(new[] { lon, lat });
        }

        return new(distance, distance / 25d * 60d, ordered, geometry);
    }

    private static bool IsValidCoordinate(double latitude, double longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && (latitude != 0 || longitude != 0);

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371d;
        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLon = (lon2 - lon1) * Math.PI / 180d;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private sealed record DeliveryPointGroup(
        Guid AddressId,
        Delivery Representative,
        CustomerAddress Address,
        IReadOnlyList<Delivery> Deliveries,
        string GroupStatus);
}
