using System.Security.Claims;
using HealthApp.Application.Abstractions;

namespace HealthApp.Api.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    private const string TenantHeader = "X-Outlet-Slug";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant, IOutletRepository outlets)
    {
        var claimValue = context.User.FindFirstValue("outlet_id");
        var userOutletId = Guid.TryParse(claimValue, out var parsedOutletId)
            ? parsedOutletId
            : (Guid?)null;

        var requestedSlug = context.Request.Headers[TenantHeader].FirstOrDefault()?.Trim();

        if (!string.IsNullOrWhiteSpace(requestedSlug))
        {
            var outlet = await outlets.GetBySlugAsync(requestedSlug.ToLowerInvariant());

            if (outlet is null)
            {
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, "The requested outlet tenant was not found.");
                return;
            }

            if (userOutletId.HasValue && userOutletId.Value != outlet.Id)
            {
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "The requested outlet tenant does not match the authenticated account.");
                return;
            }

            tenant.Set(outlet.Id, outlet.Slug);
        }
        else if (userOutletId.HasValue)
        {
            var outlet = await outlets.GetByIdAsync(userOutletId.Value);

            if (outlet is null)
            {
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "The authenticated outlet tenant is no longer available.");
                return;
            }

            tenant.Set(outlet.Id, outlet.Slug);
        }

        await next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { status, message }, context.RequestAborted);
    }
}
