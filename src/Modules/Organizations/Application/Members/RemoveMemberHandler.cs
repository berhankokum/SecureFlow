using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Application.Concurrency;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Members;

public sealed record RemoveMemberCommand(
    Guid OrganizationId,
    Guid CurrentUserId,
    Guid TargetUserId);

public sealed class RemoveMemberHandler(
    IOrganizationsDbContext dbContext,
    IClock clock)
{
    public async Task<Result> HandleAsync(
        RemoveMemberCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var actor = await dbContext.OrganizationMembers.SingleOrDefaultAsync(member =>
            member.OrganizationId == command.OrganizationId
            && member.UserId == command.CurrentUserId
            && member.RemovedAt == null,
            cancellationToken);

        if (actor is null)
        {
            return Result.Failure(OrganizationErrors.NotFound);
        }

        var target = await dbContext.OrganizationMembers.SingleOrDefaultAsync(member =>
            member.OrganizationId == command.OrganizationId
            && member.UserId == command.TargetUserId
            && member.RemovedAt == null,
            cancellationToken);

        if (target is null)
        {
            return Result.Failure(OrganizationErrors.MemberNotFound);
        }

        if (!OrganizationAccessRules.CanRemove(
                actor.UserId,
                actor.Role,
                target.UserId,
                target.Role))
        {
            return Result.Failure(OrganizationErrors.Forbidden);
        }

        if (target.Role == OrganizationRole.Owner)
        {
            var ownerCount = await dbContext.OrganizationMembers.CountAsync(member =>
                member.OrganizationId == command.OrganizationId
                && member.Role == OrganizationRole.Owner
                && member.RemovedAt == null,
                cancellationToken);

            if (ownerCount <= 1)
            {
                return Result.Failure(OrganizationErrors.LastOwner);
            }
        }

        target.Remove(clock.UtcNow);

        // BR-MEMBER-005 assignment cleanup, WorkItem/assignment modeli FAZ 7-8'de
        // eklendiğinde bu SaveChanges transaction'ına bağlanacaktır.
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (PostgresConcurrencyDetector.IsSerializationFailure(exception))
        {
            return Result.Failure(OrganizationErrors.ConcurrentMembership);
        }

        return Result.Success();
    }
}
