using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SecureFlow.Contracts.Outbox;
using SecureFlow.Modules.Outbox.Application;
namespace SecureFlow.Modules.Outbox.Infrastructure;

public sealed class DurableOutboxWriter(IServiceScopeFactory scopeFactory) : IDurableOutboxWriter
{
    public async Task AddAsync(OutboxWriteRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        var db = scope.ServiceProvider.GetRequiredService<IOutboxDbContext>();
        writer.Add(request);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
