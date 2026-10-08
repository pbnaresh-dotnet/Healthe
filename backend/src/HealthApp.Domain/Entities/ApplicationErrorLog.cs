namespace HealthApp.Domain.Entities;

public sealed class ApplicationErrorLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public string Environment { get; set; } = "";
    public string Severity { get; set; } = "Error";
    public string ErrorCode { get; set; } = "";
    public string Activity { get; set; } = "";
    public string ExceptionType { get; set; } = "";
    public string Message { get; set; } = "";
    public string InnerExceptionMessage { get; set; } = "";
    public string StackTrace { get; set; } = "";

    public string RequestPath { get; set; } = "";
    public string HttpMethod { get; set; } = "";
    public int StatusCode { get; set; }
    public string CorrelationId { get; set; } = "";
    public string TraceId { get; set; } = "";
    public long ElapsedMilliseconds { get; set; }

    public Guid? UserId { get; set; }
    public Guid? OutletId { get; set; }
    public string UserRole { get; set; } = "";
    public string TenantSlug { get; set; } = "";
    public string TenantHost { get; set; } = "";
    public string ClientIpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";

    public string Fingerprint { get; set; } = "";
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string ResolutionNotes { get; set; } = "";
}
