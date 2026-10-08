namespace HealthApp.Shared.DTOs;

public record ApplicationErrorListItemDto(
    Guid Id,
    DateTime OccurredAtUtc,
    string Severity,
    string ErrorCode,
    string Activity,
    string ExceptionType,
    string Message,
    string RequestPath,
    string HttpMethod,
    int StatusCode,
    string CorrelationId,
    long ElapsedMilliseconds,
    Guid? UserId,
    string UserName,
    string UserRole,
    Guid? OutletId,
    string OutletName,
    string TenantSlug,
    bool IsResolved);

public record ApplicationErrorDetailDto(
    Guid Id,
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
    string UserName,
    string UserRole,
    Guid? OutletId,
    string OutletName,
    string TenantSlug,
    string TenantHost,
    string ClientIpAddress,
    string UserAgent,
    string Fingerprint,
    bool IsResolved,
    DateTime? ResolvedAtUtc,
    Guid? ResolvedByUserId,
    string ResolvedByUserName,
    string ResolutionNotes);

public record ApplicationErrorOutletSummaryDto(
    Guid? OutletId,
    string OutletName,
    int ErrorCount,
    int UnresolvedCount);

public record ApplicationErrorSummaryDto(
    int TotalCount,
    int UnresolvedCount,
    int Last24HoursCount,
    IReadOnlyList<ApplicationErrorOutletSummaryDto> ByOutlet);

public record ApplicationErrorPageDto(
    IReadOnlyList<ApplicationErrorListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ApplicationErrorSummaryDto Summary);

public record ResolveApplicationErrorRequest(string ResolutionNotes = "");
