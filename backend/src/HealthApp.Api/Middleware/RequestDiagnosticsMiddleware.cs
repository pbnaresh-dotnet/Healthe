using System.Diagnostics;
using System.Security.Claims;

namespace HealthApp.Api.Middleware;

/// <summary>
/// Emits structured request diagnostics without recording query strings, bodies,
/// credentials, or other request payloads. Error details are controlled separately
/// by the configured diagnostics policy.
/// </summary>
public sealed class RequestDiagnosticsMiddleware(RequestDelegate next, ILogger<RequestDiagnosticsMiddleware> logger, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var enabled = configuration.GetValue("Diagnostics:RequestLoggingEnabled", true);
        var detailed = configuration.GetValue("Diagnostics:DetailedLoggingEnabled", false);
        var threshold = Math.Clamp(configuration.GetValue("Diagnostics:SlowRequestThresholdMs", 1000), 100, 120000);
        var correlationId = ResolveCorrelationId(context);
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var status = context.Response.StatusCode;
            var slow = stopwatch.ElapsedMilliseconds >= threshold;
            if (enabled && (status >= 400 || slow || detailed))
            {
                var route = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "/";
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
                var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
                if (status >= 500)
                    logger.LogError("HTTP request completed with server error. Method={Method} Route={Route} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} UserId={UserId} Role={Role}",
                        context.Request.Method, route, status, stopwatch.ElapsedMilliseconds, correlationId, userId, role);
                else if (status >= 400 || slow)
                    logger.LogWarning("HTTP request requires attention. Method={Method} Route={Route} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} UserId={UserId} Role={Role} Slow={Slow}",
                        context.Request.Method, route, status, stopwatch.ElapsedMilliseconds, correlationId, userId, role, slow);
                else
                    logger.LogInformation("HTTP request completed. Method={Method} Route={Route} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} UserId={UserId} Role={Role}",
                        context.Request.Method, route, status, stopwatch.ElapsedMilliseconds, correlationId, userId, role);

                if (detailed)
                    logger.LogDebug("Detailed request context. Host={Host} UserAgent={UserAgent} TraceId={TraceId}",
                        context.Request.Host.Host, context.Request.Headers.UserAgent.ToString(), context.TraceIdentifier);
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 100 && supplied.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
            return supplied;
        return ActivityTraceId.CreateRandom().ToString();
    }
}
