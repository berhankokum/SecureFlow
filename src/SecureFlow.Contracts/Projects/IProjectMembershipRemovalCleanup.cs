namespace SecureFlow.Contracts.Projects;

/// <summary>
/// Project membership removal transaction'ına tenant-owned modüllerin kendi aktif
/// ilişkilerini temizlemesini bağlar.
/// </summary>
public interface IProjectMembershipRemovalCleanup
{
    Task RemoveActiveRelationshipsAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
