using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.Organizations.Application.Abstractions;

namespace SecureFlow.Modules.Organizations.Application.Authorization;

public sealed class OrganizationPermissionAuthorizationHandler(IOrganizationsDbContext dbContext)
    : AuthorizationHandler<PermissionRequirement, OrganizationAuthorizationResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement,
        OrganizationAuthorizationResource resource)
    {
        if (!Guid.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var currentUserId))
        {
            return;
        }

        var membership = await dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(member =>
                member.OrganizationId == resource.OrganizationId
                && member.UserId == currentUserId
                && member.RemovedAt == null,
                resource.CancellationToken);

        if (membership is null)
        {
            return;
        }

        resource.MarkActiveMembership();

        var isSelfRemoval = requirement.Permission == Permissions.MemberRemove
            && resource.TargetUserId == currentUserId;

        if (isSelfRemoval
            || OrganizationAccessRules.HasPermission(membership.Role, requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
