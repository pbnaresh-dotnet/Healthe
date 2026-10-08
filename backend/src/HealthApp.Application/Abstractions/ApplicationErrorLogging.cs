using HealthApp.Domain.Entities;

namespace HealthApp.Application.Abstractions;

public sealed record ApplicationErrorLogEntry(
    DateTime OccurredAtUtc,
    string Environment,
    string Severity,
    string ErrorCode,
    string Activity,
    string ExceptionType,
    string Message,
    string InnerExceptionMessage,
    string StackTrace,
    string RequestPath,
    string HttpMethod,
    int StatusCode,
    string CorrelationId,
    string TraceId,
    long ElapsedMilliseconds,
    Guid? UserId,
    Guid? OutletId,
    string UserRole,
    string TenantSlug,
    string TenantHost,
    string ClientIpAddress,
    string UserAgent,
    string Fingerprint);

public record ApplicationErrorQueryRequest(
    Guid? OutletId = null,
    string? Severity = null,
    int? StatusCode = null,
    bool? Resolved = null,
    string? Search = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 50);

public interface IApplicationErrorLogger
{
    Task LogAsync(ApplicationErrorLogEntry entry, CancellationToken cancellationToken = default);
}

public interface IApplicationErrorRepository
{
    Task AddAsync(ApplicationErrorLog error, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ApplicationErrorLog> Items, int TotalCount)> QueryAsync(ApplicationErrorQueryRequest request, CancellationToken cancellationToken = default);
    Task<ApplicationErrorSummaryData> GetSummaryAsync(ApplicationErrorQueryRequest request, CancellationToken cancellationToken = default);
    Task<ApplicationErrorLog?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApplicationErrorLog?> ResolveAsync(Guid id, Guid resolvedByUserId, string notes, CancellationToken cancellationToken = default);
}

public sealed record ApplicationErrorSummaryData(
    int TotalCount,
    int UnresolvedCount,
    int Last24HoursCount,
    IReadOnlyList<ApplicationErrorOutletSummaryData> ByOutlet);

public sealed record ApplicationErrorOutletSummaryData(
    Guid? OutletId,
    int ErrorCount,
    int UnresolvedCount);
