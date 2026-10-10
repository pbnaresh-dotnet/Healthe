using HealthApp.Application.Abstractions;
using HealthApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/outlets/me/delivery-labels"),Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")]

public sealed class OutletLabelsController(IDeliveryLabelService labels, ICurrentUser current, IDeliveryRepository deliveries):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get([FromQuery]DateTime? date)=>Ok(await labels.GetLabelsAsync(date));

    [HttpPost("deliveries/{deliveryId:guid}/food-ready")]
    public async Task<IActionResult> MarkFoodReady(Guid deliveryId)
    {
        if (current.OutletId is not Guid outletId)
            return Unauthorized(new { message = "Outlet context is required." });

        var delivery = await deliveries.GetAsync(deliveryId);
        if (delivery is null || delivery.OutletId != outletId)
            return NotFound(new { message = "Delivery not found for this outlet." });

        if (delivery.RouteId.HasValue || delivery.Status is DeliveryStatus.PickedUp or DeliveryStatus.OutForDelivery or DeliveryStatus.Delivered)
            return Conflict(new { message = "This delivery is already assigned or has progressed beyond kitchen preparation." });

        if (delivery.Status is not (DeliveryStatus.Scheduled or DeliveryStatus.Preparing))
            return Conflict(new { message = $"Cannot mark this delivery Food Ready from status {delivery.Status}." });

        delivery.Status = DeliveryStatus.FoodReady;
        await deliveries.UpdateAsync(delivery);
        return Ok(new { deliveryId = delivery.Id, status = delivery.Status.ToString() });
    }
}
