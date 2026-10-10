using HealthApp.Application.Abstractions;
using HealthApp.Domain.Enums;
using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/outlets/me/delivery-labels"),Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")]

public sealed class OutletLabelsController(IDeliveryLabelService labels, ICurrentUser current, IDeliveryRepository deliveries, ISubscriptionMealSelectionRepository selections):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get([FromQuery]DateTime? date)=>Ok(await labels.GetLabelsAsync(date));

    [HttpPost("selections/{selectionId:guid}/food-ready")]
    public async Task<IActionResult> MarkMealFoodReady(Guid selectionId)
    {
        if (current.OutletId is not Guid outletId)
            return Unauthorized(new { message = "Outlet context is required." });

        var selection = await selections.GetAsync(selectionId);
        if (selection is null)
            return NotFound(new { message = "Meal item not found." });

        var selectionOutlet = (await selections.GetByOutletAndDateRangeAsync(outletId, selection.MealDate.Date, selection.MealDate.Date.AddDays(1)))
            .FirstOrDefault(x => x.Id == selectionId);
        if (selectionOutlet is null)
            return NotFound(new { message = "Meal item not found for this outlet." });

        if (selection.Status is not (MealSelectionStatus.Scheduled or MealSelectionStatus.Prepared))
            return Conflict(new { message = $"Cannot mark this meal Food Ready from status {selection.Status}." });

        selection.Status = MealSelectionStatus.Prepared;
        await selections.UpdateAsync(selection);

        var daySelections = await selections.GetByOutletAndDateRangeAsync(outletId, selection.MealDate.Date, selection.MealDate.Date.AddDays(1));
        var addressId = selection.AddressId;
        var itemsForDelivery = daySelections.Where(x => x.SubscriptionId == selection.SubscriptionId &&
            x.MealDate.Date == selection.MealDate.Date && x.MealSlot == selection.MealSlot &&
            x.AddressId == addressId && x.Status is MealSelectionStatus.Scheduled or MealSelectionStatus.Prepared).ToList();

        var allReady = itemsForDelivery.Count > 0 && itemsForDelivery.All(x => x.Status == MealSelectionStatus.Prepared);
        var dayDeliveries = await deliveries.GetByOutletAsync(outletId);
        var delivery = dayDeliveries.FirstOrDefault(x => x.SubscriptionId == selection.SubscriptionId &&
            x.ScheduledDate.Date == selection.MealDate.Date && x.MealSlot == selection.MealSlot &&
            x.DeliveryAddressId == addressId);
        if (delivery is not null && allReady && !delivery.RouteId.HasValue && delivery.Status is DeliveryStatus.Scheduled or DeliveryStatus.Preparing)
        {
            delivery.Status = DeliveryStatus.FoodReady;
            await deliveries.UpdateAsync(delivery);
        }
        return Ok(new { selectionId, itemStatus = selection.Status.ToString(), deliveryId = delivery?.Id,
            deliveryStatus = delivery?.Status.ToString(), allItemsReady = allReady });
    }

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
