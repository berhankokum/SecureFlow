namespace SecureFlow.Modules.Organizations.Application.Authorization;

public sealed class OrganizationAuthorizationResource(
    Guid organizationId,
    Guid? targetUserId = null,
    CancellationToken cancellationToken = default)
{
    public Guid OrganizationId { get; } = organizationId;

    public Guid? TargetUserId { get; } = targetUserId;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public bool HasActiveMembership { get; internal set; }
}
