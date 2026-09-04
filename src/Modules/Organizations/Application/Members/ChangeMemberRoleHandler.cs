using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Application.Concurrency;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Members;

public sealed record ChangeMemberRoleCommand(
    Guid OrganizationId,
    Guid CurrentUserId,
    Guid TargetUserId,
    OrganizationRole Role);

public sealed class ChangeMemberRoleHandler(IOrganizationsDbContext dbContext)
{
    public async Task<Result> HandleAsync(
        ChangeMemberRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        if (!await dbContext.LockOrganizationAsync(command.OrganizationId, cancellationToken))
        {
            return Result.Failure(OrganizationErrors.NotFound);
        }

        var actor = await dbContext.OrganizationMembers.SingleOrDefaultAsync(member =>
            member.OrganizationId == command.OrganizationId
            && member.UserId == command.CurrentUserId
            && member.RemovedAt == null,
            cancellationToken);

        if (actor is null)
        {
            return Result.Failure(OrganizationErrors.NotFound);
        }

        if (!OrganizationAccessRules.CanChangeRole(actor.Role))
        {
            return Result.Failure(OrganizationErrors.Forbidden);
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

        if (target.Role == command.Role)
        {
            return Result.Success();
        }

        if (target.Role == OrganizationRole.Owner && command.Role != OrganizationRole.Owner)
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

        target.ChangeRole(command.Role);
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
