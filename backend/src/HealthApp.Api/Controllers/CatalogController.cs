using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController,Route("api/catalog"),AllowAnonymous]
public sealed class CatalogController(ICatalogService service) : ControllerBase
{
    [HttpGet("ingredients")] public async Task<IActionResult> Ingredients()=>Ok(await service.GetIngredientsAsync());
    [HttpGet("allergens")] public async Task<IActionResult> Allergens()=>Ok(await service.GetAllergensAsync());
}