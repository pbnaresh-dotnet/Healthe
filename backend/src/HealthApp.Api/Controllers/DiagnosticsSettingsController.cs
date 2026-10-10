using System.Data;
using System.Security.Claims;
using HealthApp.Api.Middleware;
using HealthApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/admin/diagnostics/settings")]
[Authorize(Roles = "SuperAdmin")]
public sealed class DiagnosticsSettingsController(HealthAppDbContext db, DiagnosticsPolicy policy) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RequestLoggingEnabled,DetailedLoggingEnabled,SlowRequestThresholdMs,UpdatedAtUtc,UpdatedByUserId FROM dbo.DiagnosticsSettings WHERE Id=1";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return StatusCode(503, new { message = "Diagnostics settings have not been initialized." });
        var requestLogging = reader.GetBoolean(0);
        var detailedLogging = reader.GetBoolean(1);
        var threshold = reader.GetInt32(2);
        var updatedAt = reader.GetDateTime(3);
        var updatedBy = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4);
        policy.Update(requestLogging, detailedLogging, threshold);
        return Ok(new { requestLoggingEnabled = requestLogging, detailedLoggingEnabled = detailedLogging,
            slowRequestThresholdMs = threshold, updatedAtUtc = updatedAt, updatedByUserId = updatedBy });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateDiagnosticsSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.SlowRequestThresholdMs is < 100 or > 120000)
            return BadRequest(new { message = "Slow request threshold must be between 100 and 120000 milliseconds." });
        var actorId = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (Guid?)null;
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE dbo.DiagnosticsSettings
SET RequestLoggingEnabled=@requestLogging,DetailedLoggingEnabled=@detailedLogging,
    SlowRequestThresholdMs=@threshold,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedByUserId=@actor WHERE Id=1";
        Add(command, "@requestLogging", request.RequestLoggingEnabled);
        Add(command, "@detailedLogging", request.DetailedLoggingEnabled);
        Add(command, "@threshold", request.SlowRequestThresholdMs);
        Add(command, "@actor", actorId.HasValue ? actorId.Value : DBNull.Value);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed == 0)
            return StatusCode(503, new { message = "Diagnostics settings have not been initialized." });
        policy.Update(request.RequestLoggingEnabled, request.DetailedLoggingEnabled, request.SlowRequestThresholdMs);
        return Ok(new { requestLoggingEnabled = request.RequestLoggingEnabled,
            detailedLoggingEnabled = request.DetailedLoggingEnabled,
            slowRequestThresholdMs = request.SlowRequestThresholdMs, updatedAtUtc = DateTime.UtcNow, updatedByUserId = actorId });
    }

    private static void Add(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

public sealed record UpdateDiagnosticsSettingsRequest(
    bool RequestLoggingEnabled,
    bool DetailedLoggingEnabled,
    int SlowRequestThresholdMs);
