using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Api.Persistence;

namespace SecureFlow.Api.Realtime;

[Authorize]
public sealed class RealtimeHub(SecureFlowDbContext dbContext) : Hub
{
    private const string SubscriptionDeniedMessage = "Subscription denied.";

    public override async Task OnConnectedAsync()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroupNames.User(userId));
        await base.OnConnectedAsync();
    }

    public async Task SubscribeOrganization(Guid organizationId)
    {
        var userId = GetCurrentUserId();
        if (organizationId == Guid.Empty || !await dbContext.IsActiveOrganizationMemberAsync(
                organizationId,
                userId,
                Context.ConnectionAborted))
        {
            throw new HubException(SubscriptionDeniedMessage);
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroupNames.Organization(organizationId),
            Context.ConnectionAborted);
    }

    public async Task SubscribeProject(Guid projectId)
    {
        var userId = GetCurrentUserId();
        var scope = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new { project.Id, project.OrganizationId })
            .SingleOrDefaultAsync(Context.ConnectionAborted);
        if (scope is null || !await dbContext.CanUserAccessProjectAsync(
                scope.Id,
                scope.OrganizationId,
                userId,
                Context.ConnectionAborted))
        {
            throw new HubException(SubscriptionDeniedMessage);
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroupNames.Project(projectId),
            Context.ConnectionAborted);
    }

    public Task UnsubscribeOrganization(Guid organizationId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroupNames.Organization(organizationId),
            Context.ConnectionAborted);

    public Task UnsubscribeProject(Guid projectId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroupNames.Project(projectId),
            Context.ConnectionAborted);

    private Guid GetCurrentUserId() => TryGetCurrentUserId(out var userId)
        ? userId
        : throw new HubException(SubscriptionDeniedMessage);

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
