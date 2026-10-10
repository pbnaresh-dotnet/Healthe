using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthApp.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HealthApp.Api.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    DiagnosticsPolicy diagnosticsPolicy)
{
    public async Task InvokeAsync(
        HttpContext context,
        IApplicationErrorLogger applicationErrorLogger,
        ICurrentUser currentUser,
        ITenantContext tenant,
        IHostEnvironment hostEnvironment)
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
            await LogExceptionAsync(context, ex, 404, correlationId, stopwatch, applicationErrorLogger, currentUser, tenant, hostEnvironment);
            await Write(context, 404, ex.Message, correlationId);
        }
        catch (UnauthorizedAccessException ex)
        {
            exceptionLogged = true;
            await LogExceptionAsync(context, ex, 401, correlationId, stopwatch, applicationErrorLogger, currentUser, tenant, hostEnvironment);
            await Write(context, 401, ex.Message, correlationId);
        }
        catch (ArgumentException ex)
        {
            exceptionLogged = true;
            await LogExceptionAsync(context, ex, 400, correlationId, stopwatch, applicationErrorLogger, currentUser, tenant, hostEnvironment);
            await Write(context, 400, ex.Message, correlationId);
        }
        catch (InvalidOperationException ex)
        {
            exceptionLogged = true;
            await LogExceptionAsync(context, ex, 409, correlationId, stopwatch, applicationErrorLogger, currentUser, tenant, hostEnvironment);
            await Write(context, 409, ex.Message, correlationId);
        }
        catch (Exception ex)
        {
            exceptionLogged = true;
            if (diagnosticsPolicy.Current.DetailedLoggingEnabled)
                logger.LogError(ex, "Unhandled API exception. CorrelationId={CorrelationId}", correlationId);
            else
                logger.LogError("Unhandled API exception. ExceptionType={ExceptionType} CorrelationId={CorrelationId}", ex.GetType().Name, correlationId);
            await LogExceptionAsync(context, ex, 500, correlationId, stopwatch, applicationErrorLogger, currentUser, tenant, hostEnvironment);
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
                await LogResponseAsync(context, correlationId, stopwatch, applicationErrorLogger, currentUser, tenant, hostEnvironment);
            }
        }
    }

    private async Task LogExceptionAsync(
        HttpContext context,
        Exception exception,
        int statusCode,
        string correlationId,
        Stopwatch stopwatch,
        IApplicationErrorLogger applicationErrorLogger,
        ICurrentUser currentUser,
        ITenantContext tenant,
        IHostEnvironment hostEnvironment)
    {
        var detailed = diagnosticsPolicy.Current.DetailedLoggingEnabled;
        var message = detailed ? (exception.Message ?? exception.GetType().Name) : "Request failed; use the correlation ID to investigate.";
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
                detailed ? (exception.InnerException?.Message ?? "") : "",
                detailed ? (exception.StackTrace ?? "") : "",
                statusCode,
                correlationId,
                stopwatch.ElapsedMilliseconds,
                outletId,
                tenantSlug,
                hostEnvironment.EnvironmentName));
    }

    private async Task LogResponseAsync(
        HttpContext context,
        string correlationId,
        Stopwatch stopwatch,
        IApplicationErrorLogger applicationErrorLogger,
        ICurrentUser currentUser,
        ITenantContext tenant,
        IHostEnvironment hostEnvironment)
    {
        var statusCode = context.Response.StatusCode;
        var severity = statusCode >= 500 ? "Error" : "Warning";
        var outletId = currentUser.OutletId ?? tenant.OutletId;
        var tenantSlug = tenant.OutletSlug ?? "";
        var activity = context.GetEndpoint()?.DisplayName
            ?? $"{context.Request.Method} {context.Request.Path}";
        var validationDetails = context.Items.TryGetValue("HealthApp.ModelValidationErrors", out var validationValue)
            ? validationValue?.ToString()
            : null;
        var message = diagnosticsPolicy.Current.DetailedLoggingEnabled && !string.IsNullOrWhiteSpace(validationDetails)
            ? $"Request returned HTTP {statusCode}. Validation: {validationDetails}"
            : $"Request returned HTTP {statusCode}.";
        var errorCode = !string.IsNullOrWhiteSpace(validationDetails)
            ? "MODEL_VALIDATION"
            : $"HTTP_{statusCode}";

        await applicationErrorLogger.LogAsync(
            BuildEntry(
                context,
                severity,
                errorCode,
                activity,
                "",
                message,
                "",
                "",
                statusCode,
                correlationId,
                stopwatch.ElapsedMilliseconds,
                outletId,
                tenantSlug,
                hostEnvironment.EnvironmentName));
    }

    private ApplicationErrorLogEntry BuildEntry(
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
        string tenantSlug,
        string environment)
    {
        var userId = Guid.TryParse(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var parsedUserId)
            ? parsedUserId
            : (Guid?)null;

        var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        var detailed = diagnosticsPolicy.Current.DetailedLoggingEnabled;
        var host = detailed ? context.Request.Host.Host ?? "" : "";
        var clientIp = detailed ? context.Connection.RemoteIpAddress?.ToString() ?? "" : "";
        var userAgent = detailed ? context.Request.Headers.UserAgent.ToString() : "";
        var path = context.Request.Path.Value ?? "/";
        var fingerprint = Fingerprint(errorCode, path, exceptionType, message);

        return new ApplicationErrorLogEntry(
            DateTime.UtcNow,
            environment,
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
            clientIp,
            userAgent,
            fingerprint);
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            var candidate = supplied.Trim();
            if (candidate.Length <= 100 && candidate.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
                return candidate;
        }

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
