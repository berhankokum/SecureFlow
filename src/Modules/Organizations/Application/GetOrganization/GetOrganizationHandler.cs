using SecureFlow.Modules.Organizations.Application.Models;

namespace SecureFlow.Modules.Organizations.Application.GetOrganization;

public sealed class GetOrganizationHandler(OrganizationReadService reader)
{
    public async Task<OrganizationSummary?> HandleAsync(Guid organizationId, Guid currentUserId,
        CancellationToken cancellationToken = default) =>
        (await reader.ReadAsync(currentUserId, organizationId, cancellationToken)).SingleOrDefault();
}
