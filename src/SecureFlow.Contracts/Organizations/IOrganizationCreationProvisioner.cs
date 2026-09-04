namespace SecureFlow.Contracts.Organizations;

public interface IOrganizationCreationProvisioner
{
    Task ProvisionAsync(
        Guid organizationId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}
