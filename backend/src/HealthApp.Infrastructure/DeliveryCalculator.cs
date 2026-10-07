using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Infrastructure;

public sealed class DeliveryCalculator(
    IOutletRepository outlets,
    ICustomerAddressRepository addresses,
    IDeliveryPricingRepository pricing,
    IOutletDeliveryAreaRepository outletAreas) : IDeliveryCalculator
{
    public async Task<DeliveryQuoteDto> QuoteAsync(Guid outletId, Guid customerId, Guid addressId)
    {
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        var address = await addresses.GetAsync(customerId, addressId);
        if (address is null)
        {
            throw new KeyNotFoundException("Address not found.");
        }
        if (string.IsNullOrWhiteSpace(address.City) ||
            !address.City.Equals(outlet.City, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"This package is for {outlet.City}, but the selected address is in {address.City}.");

        if (double.IsNaN(outlet.Latitude) || double.IsInfinity(outlet.Latitude) ||
            double.IsNaN(outlet.Longitude) || double.IsInfinity(outlet.Longitude))
            throw new InvalidOperationException("The outlet has invalid map coordinates.");
        if (double.IsNaN(address.Latitude) || double.IsInfinity(address.Latitude) || address.Latitude is < -90 or > 90 || double.IsNaN(address.Longitude) || double.IsInfinity(address.Longitude) || address.Longitude is < -180 or > 180) throw new InvalidOperationException("The delivery address has invalid map coordinates.");
        var distance = DistanceKm(outlet.Latitude, outlet.Longitude, address.Latitude, address.Longitude);
        if (outlet.DeliveryCoverageMode == DeliveryCoverageMode.Areas)
        {
            if (!address.CityAreaId.HasValue)
                throw new InvalidOperationException("This outlet delivers by selected service areas. Please choose an address inside a configured delivery area.");
            var selectedAreas = await outletAreas.GetByOutletAsync(outletId);
            if (!selectedAreas.Any(x => x.CityAreaId == address.CityAreaId.Value && x.IsActive))
                throw new InvalidOperationException("This address is outside the outlet's selected delivery areas.");
        }
        else if (distance > outlet.ServiceRadiusKm)
        {
            throw new InvalidOperationException("Address is outside the outlet service radius.");
        }
        var rules = await pricing.GetByOutletAsync(outletId);
        var rule = rules.FirstOrDefault(x => distance <= (double)x.MaxDistanceKm);
        if (rule is null) throw new InvalidOperationException("No delivery pricing slab covers this address distance.");
        return new(address.Id, Math.Round(distance,2), rule.Fee, address.Locality);
    }

    public async Task<decimal> CalculateForSelectionsAsync(Guid outletId, Guid customerId, SubscriptionDeliveryMode mode, IReadOnlyList<SubscriptionMealSelection> selections)
    {
        if (selections.Count == 0) return 0m;
        var addressCache = new Dictionary<Guid, DeliveryQuoteDto>();
        foreach (var s in selections)
        {
            if (!s.AddressId.HasValue) throw new InvalidOperationException("Every scheduled meal requires a delivery address.");
            if (!addressCache.ContainsKey(s.AddressId.Value)) addressCache[s.AddressId.Value] = await QuoteAsync(outletId, customerId, s.AddressId.Value);
        }
        return mode == SubscriptionDeliveryMode.OneDeliveryPerDay
            ? selections.GroupBy(x => x.MealDate.Date).Sum(g => addressCache[g.First().AddressId!.Value].DeliveryFee)
            : selections.Sum(x => addressCache[x.AddressId!.Value].DeliveryFee);
    }

    private static double DistanceKm(double lat1,double lon1,double lat2,double lon2)
    {
        const double R=6371d; var dLat=(lat2-lat1)*Math.PI/180d; var dLon=(lon2-lon1)*Math.PI/180d;
        var a=Math.Sin(dLat/2)*Math.Sin(dLat/2)+Math.Cos(lat1*Math.PI/180d)*Math.Cos(lat2*Math.PI/180d)*Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return R*2*Math.Atan2(Math.Sqrt(a),Math.Sqrt(1-a));
    }
}
