using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthApp.Application.Abstractions;

namespace HealthApp.Api.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IApplicationErrorLogger applicationErrorLogger,
    ICurrentUser currentUser,
    ITenantContext tenant)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = GetCorrelationId(context);
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        var exceptionLogged = false;

        try
        {
            await next(context);
        }
        catch (KeyNotFoundException ex)
        {
            exceptionLogged = true;
            await LogExceptionAsync(context, ex, 404, correlationId, stopwatch);
            await Write(context, 404, ex.Message, correlationId);
        }
        catch (UnauthorizedAccessException ex)
        {
            exceptionLogged = true;
            await LogExceptionAsync(context, ex, 401, correlationId, stopwatch);
            await Write(context, 401, ex.Message, correlationId);
        }
        catch (ArgumentException ex)
        {
            exceptionLogged = true;
            await LogExceptionAsync(context, ex, 400, correlationId, stopwatch);
            await Write(context, 400, ex.Message, correlationId);
        }
        catch (InvalidOperationException ex)
        {
            exceptionLogged = true;
            await LogExceptionAsync(context, ex, 409, correlationId, stopwatch);
            await Write(context, 409, ex.Message, correlationId);
        }
        catch (Exception ex)
        {
            exceptionLogged = true;
            logger.LogError(ex, "Unhandled API exception. CorrelationId={CorrelationId}", correlationId);
            await LogExceptionAsync(context, ex, 500, correlationId, stopwatch);
            await Write(context, 500, "An unexpected error occurred.", correlationId);
        }
        finally
        {
            stopwatch.Stop();

            // Also persist framework-generated 4xx/5xx responses such as
            // model-validation 400s, authorization 403s, rate limits and 404s
            // that do not throw an application exception.
            if (!exceptionLogged && context.Response.StatusCode >= 400)
            {
                await LogResponseAsync(context, correlationId, stopwatch);
            }
        }
    }

    private async Task LogExceptionAsync(
        HttpContext context,
        Exception exception,
        int statusCode,
        string correlationId,
        Stopwatch stopwatch)
    {
        var message = exception.Message ?? exception.GetType().Name;
        var errorCode = "EXC_" + exception.GetType().Name;
        var outletId = currentUser.OutletId ?? tenant.OutletId;
        var tenantSlug = tenant.OutletSlug ?? "";
        var activity = context.GetEndpoint()?.DisplayName
            ?? $"{context.Request.Method} {context.Request.Path}";

        await applicationErrorLogger.LogAsync(
            BuildEntry(
                context,
                "Error",
                errorCode,
                activity,
                exception.GetType().FullName ?? exception.GetType().Name,
                message,
                exception.InnerException?.Message ?? "",
                exception.StackTrace ?? "",
                statusCode,
                correlationId,
                stopwatch.ElapsedMilliseconds,
                outletId,
                tenantSlug));
    }

    private async Task LogResponseAsync(
        HttpContext context,
        string correlationId,
        Stopwatch stopwatch)
    {
        var statusCode = context.Response.StatusCode;
        var severity = statusCode >= 500 ? "Error" : "Warning";
        var outletId = currentUser.OutletId ?? tenant.OutletId;
        var tenantSlug = tenant.OutletSlug ?? "";
        var activity = context.GetEndpoint()?.DisplayName
            ?? $"{context.Request.Method} {context.Request.Path}";
        var message = $"Request returned HTTP {statusCode}.";

        await applicationErrorLogger.LogAsync(
            BuildEntry(
                context,
                severity,
                $"HTTP_{statusCode}",
                activity,
                "",
                message,
                "",
                "",
                statusCode,
                correlationId,
                stopwatch.ElapsedMilliseconds,
                outletId,
                tenantSlug));
    }

    private static ApplicationErrorLogEntry BuildEntry(
        HttpContext context,
        string severity,
        string errorCode,
        string activity,
        string exceptionType,
        string message,
        string innerExceptionMessage,
        string stackTrace,
        int statusCode,
        string correlationId,
        long elapsedMilliseconds,
        Guid? outletId,
        string tenantSlug)
    {
        var userId = Guid.TryParse(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var parsedUserId)
            ? parsedUserId
            : (Guid?)null;

        var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        var host = context.Request.Host.Host ?? "";
        var path = context.Request.Path.Value ?? "/";
        var fingerprint = Fingerprint(errorCode, path, exceptionType, message);

        return new ApplicationErrorLogEntry(
            DateTime.UtcNow,
            context.RequestServices.GetRequiredService<IHostEnvironment>().EnvironmentName,
            severity,
            errorCode,
            activity,
            exceptionType,
            message,
            innerExceptionMessage,
            stackTrace,
            path,
            context.Request.Method,
            statusCode,
            correlationId,
            Activity.Current?.Id ?? context.TraceIdentifier,
            elapsedMilliseconds,
            userId,
            outletId,
            role,
            tenantSlug,
            host,
            context.Connection.RemoteIpAddress?.ToString() ?? "",
            context.Request.Headers.UserAgent.ToString(),
            fingerprint);
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(supplied))
            return supplied.Trim()[..Math.Min(100, supplied.Trim().Length)];

        return context.TraceIdentifier;
    }

    private static string Fingerprint(
        string errorCode,
        string path,
        string exceptionType,
        string message)
    {
        var raw = $"{errorCode}|{path}|{exceptionType}|{message}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task Write(
        HttpContext context,
        int status,
        string message,
        string correlationId)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status,
            message,
            correlationId
        }));
    }
}
