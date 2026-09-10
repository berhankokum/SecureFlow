using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Modules.Outbox.Domain;
namespace SecureFlow.Modules.Outbox.Application;

public interface IOutboxDbContext { DbSet<OutboxMessage> OutboxMessages { get; } DatabaseFacade Database { get; } Task<int> SaveChangesAsync(CancellationToken cancellationToken = default); }
