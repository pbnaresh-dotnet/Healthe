using HealthApp.Domain.Enums;

namespace HealthApp.Shared.DTOs;

public record OutletDomainDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    string Hostname,
    string Type,
    string Status,
    bool IsPrimary,
    DateTime CreatedAtUtc,
    DateTime? VerifiedAtUtc,
    string VerificationRecordType,
    string VerificationName,
    string VerificationValue);

public record RequestOutletDomainRequest(string Hostname, bool IsPrimary = false);
public record SetOutletDomainStatusRequest(string Status);
