using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthApp.Infrastructure.Logging;

public sealed class SqlApplicationErrorLogger(
    IServiceScopeFactory scopeFactory,
    ILogger<SqlApplicationErrorLogger> fallbackLogger) : IApplicationErrorLogger
{
    public async Task LogAsync(ApplicationErrorLogEntry entry, CancellationToken cancellationToken = default)
    {
        var error = new ApplicationErrorLog
        {
            OccurredAtUtc = entry.OccurredAtUtc,
            Environment = entry.Environment,
            Severity = entry.Severity,
            ErrorCode = entry.ErrorCode,
            Activity = entry.Activity,
            ExceptionType = entry.ExceptionType,
            Message = entry.Message,
            InnerExceptionMessage = entry.InnerExceptionMessage,
            StackTrace = entry.StackTrace,
            RequestPath = entry.RequestPath,
            HttpMethod = entry.HttpMethod,
            StatusCode = entry.StatusCode,
            CorrelationId = entry.CorrelationId,
            TraceId = entry.TraceId,
            ElapsedMilliseconds = entry.ElapsedMilliseconds,
            UserId = entry.UserId,
            OutletId = entry.OutletId,
            UserRole = entry.UserRole,
            TenantSlug = entry.TenantSlug,
            TenantHost = entry.TenantHost,
            ClientIpAddress = entry.ClientIpAddress,
            UserAgent = entry.UserAgent,
            Fingerprint = entry.Fingerprint
        };

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IApplicationErrorRepository>();
            await repository.AddAsync(error, cancellationToken);
        }
        catch (Exception ex)
        {
            // Error logging must never break the original request or create a logging loop.
            fallbackLogger.LogCritical(
                ex,
                "Unable to persist ApplicationErrorLog. OriginalErrorCode={ErrorCode}, CorrelationId={CorrelationId}, Message={Message}",
                entry.ErrorCode,
                entry.CorrelationId,
                entry.Message);
        }
    }
}
