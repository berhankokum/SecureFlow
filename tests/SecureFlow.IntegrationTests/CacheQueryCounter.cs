using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SecureFlow.IntegrationTests;

public sealed class CacheQueryCounter : DbCommandInterceptor
{
    private int _settingsLoads;
    public int SettingsLoads => Volatile.Read(ref _settingsLoads);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("AND xmin::text =", StringComparison.Ordinal))
            Interlocked.Increment(ref _settingsLoads);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
