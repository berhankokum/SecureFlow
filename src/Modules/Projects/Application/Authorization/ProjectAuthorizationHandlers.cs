using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SecureFlow.Contracts.Authorization;

namespace SecureFlow.Modules.Projects.Application.Authorization;

public sealed class ProjectAccessAuthorizationHandler(ProjectAuthorizationEvaluator evaluator)
    : AuthorizationHandler<ProjectAccessRequirement, ProjectAuthorizationResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectAccessRequirement requirement,
        ProjectAuthorizationResource resource)
    {
        var evaluation = await EvaluateAsync(context, resource);
        if (evaluation?.HasProjectAccess == true)
        {
            resource.MarkProjectAccess();
            context.Succeed(requirement);
        }
    }

    private async Task<ProjectAccessEvaluation?> EvaluateAsync(
        AuthorizationHandlerContext context,
        ProjectAuthorizationResource resource)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return null;
        }

        var evaluation = await evaluator.EvaluateAsync(
            resource.ProjectId,
            resource.OrganizationId,
            userId,
            resource.CancellationToken);
        if (evaluation is not null)
        {
            resource.MarkActiveOrganizationMembership();
        }

        return evaluation;
    }
}

public sealed class ProjectPermissionAuthorizationHandler(ProjectAuthorizationEvaluator evaluator)
    : AuthorizationHandler<PermissionRequirement, ProjectAuthorizationResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement,
        ProjectAuthorizationResource resource)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        var evaluation = await evaluator.EvaluateAsync(
            resource.ProjectId,
            resource.OrganizationId,
            userId,
            resource.CancellationToken);
        if (evaluation is null)
        {
            return;
        }

        resource.MarkActiveOrganizationMembership();
        if (!evaluation.HasProjectAccess)
        {
            return;
        }

        resource.MarkProjectAccess();
        if (evaluation.Permissions.Contains(requirement.Permission, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }
    }
}
