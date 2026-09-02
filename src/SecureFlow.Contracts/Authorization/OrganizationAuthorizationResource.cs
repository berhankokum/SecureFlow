namespace SecureFlow.Contracts.Authorization;

/// <summary>
/// Organization kapsamlı authorization handler'ları için modüller arası resource sözleşmesi.
/// </summary>
public sealed class OrganizationAuthorizationResource(
    Guid organizationId,
    Guid? targetUserId = null,
    CancellationToken cancellationToken = default)
{
    public Guid OrganizationId { get; } = organizationId;

    public Guid? TargetUserId { get; } = targetUserId;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public bool HasActiveMembership { get; private set; }

    public void MarkActiveMembership() => HasActiveMembership = true;
}
