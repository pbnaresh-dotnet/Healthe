using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;
using HealthApp.Shared.DTOs;

namespace HealthApp.Infrastructure;

public sealed class DeliveryCalculator(
    IOutletRepository outlets,
    ICustomerAddressRepository addresses,
    IOutletDeliveryAreaRepository deliveryAreas,
    IDeliveryPricingRepository pricing) : IDeliveryCalculator
{
    public async Task<DeliveryQuoteDto> QuoteAsync(Guid outletId, Guid customerId, Guid addressId)
    {
        var outlet = await outlets.GetByIdAsync(outletId) ?? throw new KeyNotFoundException("Outlet not found.");
        var address = await addresses.GetAsync(customerId, addressId);
        if (address is null)
        {
            throw new KeyNotFoundException("Address not found.");
        }
        var areas = await deliveryAreas.GetAreasForOutletAsync(outletId);
        var area = areas.FirstOrDefault(x => x.Id == address.CityAreaId);
        if (area is null) throw new InvalidOperationException("The selected address area is not serviced by this outlet.");
        if (!area.City.Equals(outlet.City, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"This package is for {outlet.City}, but the selected address is in {area.City}.");
        if (double.IsNaN(address.Latitude) || double.IsInfinity(address.Latitude) || address.Latitude is < -90 or > 90 || double.IsNaN(address.Longitude) || double.IsInfinity(address.Longitude) || address.Longitude is < -180 or > 180) throw new InvalidOperationException("The delivery address has invalid map coordinates.");
        var distance = DistanceKm(outlet.Latitude, outlet.Longitude, address.Latitude, address.Longitude);
        if (distance > outlet.ServiceRadiusKm) throw new InvalidOperationException("Address is outside the outlet service radius.");
        var rules = await pricing.GetByOutletAsync(outletId);
        var rule = rules.FirstOrDefault(x => distance <= (double)x.MaxDistanceKm);
        if (rule is null) throw new InvalidOperationException("No delivery pricing slab covers this address distance.");
        return new(address.Id, Math.Round(distance,2), rule.Fee, area.Name);
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
