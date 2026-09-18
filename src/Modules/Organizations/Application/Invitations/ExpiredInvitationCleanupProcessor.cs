using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Invitations;

public sealed class ExpiredInvitationCleanupProcessor(
    IOrganizationsDbContext dbContext,
    IClock clock)
{
    public async Task<int> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        using var telemetry = SecureFlow.Contracts.Observability.FlowTelemetry.StartJob("invitation_cleanup");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        var invitations = await dbContext.OrganizationInvitations
            .FromSqlInterpolated($"""
                SELECT * FROM "OrganizationInvitations"
                WHERE "AcceptedAt" IS NULL
                  AND "RevokedAt" IS NULL
                  AND "ExpiredAt" IS NULL
                  AND "ExpiresAt" <= {now}
                ORDER BY "ExpiresAt", "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT {batchSize}
                """)
            .ToArrayAsync(cancellationToken);

        var updatedCount = invitations.Count(invitation => invitation.MarkExpired(now));
        if (updatedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        telemetry.Complete();
        return updatedCount;
    }
}
