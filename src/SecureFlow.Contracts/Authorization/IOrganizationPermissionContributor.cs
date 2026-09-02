namespace SecureFlow.Contracts.Authorization;

/// <summary>
/// Project gibi alt kapsamların Organization-scoped permission katkılarını sağlar.
/// Resource-specific permission'lar bu sözleşmeden döndürülmez.
/// </summary>
public interface IOrganizationPermissionContributor
{
    Task<IReadOnlyCollection<string>> GetOrganizationScopedPermissionsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
