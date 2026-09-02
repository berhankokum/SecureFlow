namespace SecureFlow.Contracts.Authorization;

/// <summary>Project-scoped authorization için modüller arası resource sözleşmesi.</summary>
public sealed class ProjectAuthorizationResource(
    Guid projectId,
    Guid organizationId,
    CancellationToken cancellationToken = default)
{
    public Guid ProjectId { get; } = projectId;

    public Guid OrganizationId { get; } = organizationId;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public bool HasActiveOrganizationMembership { get; private set; }

    public bool HasProjectAccess { get; private set; }

    public void MarkActiveOrganizationMembership() => HasActiveOrganizationMembership = true;

    public void MarkProjectAccess() => HasProjectAccess = true;
}
