using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SecureFlow.IntegrationTests;

public sealed class CacheQueryCounter : DbCommandInterceptor
{
    private int _settingsLoads;
    private int _dashboardLoads;
    public int DashboardLoads => Volatile.Read(ref _dashboardLoads);
    public int SettingsLoads => Volatile.Read(ref _settingsLoads);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("AND xmin::text =", StringComparison.Ordinal))
            Interlocked.Increment(ref _settingsLoads);
        if (command.CommandText.Contains("Dashboard projection", StringComparison.Ordinal))
            Interlocked.Increment(ref _dashboardLoads);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
