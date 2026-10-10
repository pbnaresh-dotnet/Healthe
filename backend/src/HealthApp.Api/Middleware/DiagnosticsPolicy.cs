namespace HealthApp.Api.Middleware;

/// <summary>Runtime diagnostics policy. Defaults keep verbose exception details disabled.</summary>
public sealed class DiagnosticsPolicy
{
    private volatile Snapshot _current = new(true, false, 1000);
    public Snapshot Current => _current;
    public void Update(bool requestLoggingEnabled, bool detailedLoggingEnabled, int slowRequestThresholdMs)
        => _current = new Snapshot(requestLoggingEnabled, detailedLoggingEnabled, Math.Clamp(slowRequestThresholdMs, 100, 120000));
    public sealed record Snapshot(bool RequestLoggingEnabled, bool DetailedLoggingEnabled, int SlowRequestThresholdMs);
}
