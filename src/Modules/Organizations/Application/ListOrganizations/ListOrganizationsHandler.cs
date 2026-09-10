using SecureFlow.Modules.Organizations.Application.GetOrganization;
using SecureFlow.Modules.Organizations.Application.Models;

namespace SecureFlow.Modules.Organizations.Application.ListOrganizations;

public sealed class ListOrganizationsHandler(OrganizationReadService reader)
{
    public Task<IReadOnlyList<OrganizationSummary>> HandleAsync(Guid currentUserId,
        CancellationToken cancellationToken = default) => reader.ReadAsync(currentUserId, null, cancellationToken);
}
