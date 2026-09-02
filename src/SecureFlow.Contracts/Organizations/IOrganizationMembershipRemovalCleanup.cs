namespace SecureFlow.Contracts.Organizations;

/// <summary>
/// Organization membership removal transaction'ına tenant-owned modüllerin
/// kendi aktif ilişkilerini temizlemesini bağlar.
/// </summary>
public interface IOrganizationMembershipRemovalCleanup
{
    Task RemoveActiveRelationshipsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
