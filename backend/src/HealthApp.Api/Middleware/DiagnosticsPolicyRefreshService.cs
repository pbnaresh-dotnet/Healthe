using System.Data;
using HealthApp.Infrastructure.Data;

namespace HealthApp.Api.Middleware;

/// <summary>
/// Refreshes the persisted diagnostics policy across API instances so a Super Admin
/// change is not limited to the instance that handled the settings update.
/// </summary>
public sealed class DiagnosticsPolicyRefreshService(
    IServiceScopeFactory scopeFactory,
    DiagnosticsPolicy policy,
    ILogger<DiagnosticsPolicyRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAsync(stoppingToken);
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HealthAppDbContext>();
            await using var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT RequestLoggingEnabled,DetailedLoggingEnabled,SlowRequestThresholdMs FROM dbo.DiagnosticsSettings WHERE Id=1";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                policy.Update(reader.GetBoolean(0), reader.GetBoolean(1), reader.GetInt32(2));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal service shutdown.
        }
        catch
        {
            // Keep the last known policy. Do not log provider exception details that
            // could contain connection or infrastructure information.
            logger.LogWarning("Could not refresh diagnostics settings from the database; the last known policy remains active.");
        }
    }
}
