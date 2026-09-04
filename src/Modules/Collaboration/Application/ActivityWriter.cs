using SecureFlow.Contracts.Activity;
using SecureFlow.Modules.Collaboration.Application.Abstractions;
using SecureFlow.Modules.Collaboration.Domain;

namespace SecureFlow.Modules.Collaboration.Application;

public sealed class ActivityWriter(ICollaborationDbContext dbContext) : IActivityWriter
{
    public void Add(ActivityWriteRequest request) =>
        dbContext.ActivityEvents.Add(ActivityEvent.Create(
            request.OrganizationId,
            request.ProjectId,
            request.WorkItemId,
            request.ActorId,
            request.Type,
            request.Metadata,
            request.CreatedAt));
}
