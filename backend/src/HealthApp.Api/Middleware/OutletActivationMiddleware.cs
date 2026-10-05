using HealthApp.Domain.Enums;
using HealthApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Api.Middleware;

public sealed class OutletActivationMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedPrefixes =
    [
        "/api/auth",
        "/api/outlet-demo",
        "/api/outlet-onboarding"
    ];

    public async Task InvokeAsync(HttpContext context, HealthAppDbContext db)
    {
        var path = context.Request.Path.Value ?? "";
        if (AllowedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.IsInRole(nameof(UserRole.OutletAdmin)) &&
            !path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
        {
            var outletClaim = context.User.FindFirst("outlet_id")?.Value;
            if (Guid.TryParse(outletClaim, out var outletId))
            {
                var outletStatus = await db.Outlets
                    .Where(x => x.Id == outletId)
                    .Select(x => x.Status)
                    .FirstOrDefaultAsync(context.RequestAborted);

                if (outletStatus is not (OutletStatus.Active or OutletStatus.Live))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        status = 403,
                        message = "Your outlet is not activated yet. Complete verification and wait for HealthApp approval before using the outlet workspace."
                    }, context.RequestAborted);
                    return;
                }
            }
        }

        await next(context);
    }
}
