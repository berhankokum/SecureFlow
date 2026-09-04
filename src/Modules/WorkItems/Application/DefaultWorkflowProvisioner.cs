using SecureFlow.Contracts.Organizations;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Application;

public sealed class DefaultWorkflowProvisioner(IWorkItemsDbContext dbContext)
    : IOrganizationCreationProvisioner
{
    public Task ProvisionAsync(
        Guid organizationId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.Workflows.Add(Workflow.CreateDefault(organizationId, createdAt));
        return Task.CompletedTask;
    }
}
