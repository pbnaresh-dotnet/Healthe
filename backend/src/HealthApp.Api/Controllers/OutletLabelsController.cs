using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HealthApp.Api.Controllers;
[ApiController,Route("api/outlets/me/delivery-labels"),Authorize(Roles="OutletAdmin,OutletManager,KitchenStaff")]

public sealed class OutletLabelsController(IDeliveryLabelService labels):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get([FromQuery]DateTime? date)=>Ok(await labels.GetLabelsAsync(date));
}
