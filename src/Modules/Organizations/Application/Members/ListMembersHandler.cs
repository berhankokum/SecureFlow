using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Identity;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Members;

public sealed class ListMembersHandler(
    IOrganizationsDbContext dbContext,
    IUserDirectory userDirectory)
{
    public async Task<Result<IReadOnlyList<OrganizationMemberSummary>>> HandleAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(member =>
                member.OrganizationId == organizationId
                && member.UserId == currentUserId
                && member.RemovedAt == null,
                cancellationToken);

        if (actor is null || !OrganizationAccessRules.CanViewMembers(actor.Role))
        {
            return Result.Failure<IReadOnlyList<OrganizationMemberSummary>>(OrganizationErrors.NotFound);
        }

        var members = await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId && member.RemovedAt == null)
            .OrderBy(member => member.JoinedAt)
            .ToArrayAsync(cancellationToken);

        var emails = await userDirectory.GetEmailsByUserIdsAsync(
            members.Select(member => member.UserId).ToArray(),
            cancellationToken);

        var result = members.Select(member => new OrganizationMemberSummary(
            member.Id,
            member.UserId,
            emails.GetValueOrDefault(member.UserId, "Bilinmeyen kullanıcı"),
            member.Role.ToString().ToUpperInvariant(),
            member.JoinedAt,
            member.InvitedBy)).ToArray();

        return Result.Success<IReadOnlyList<OrganizationMemberSummary>>(result);
    }
}
