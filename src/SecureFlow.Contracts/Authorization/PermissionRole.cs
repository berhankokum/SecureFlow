namespace SecureFlow.Contracts.Authorization;

/// <summary>
/// Permission evaluation roles. Organization and project domain roles are mapped
/// to this shared representation at their module boundaries.
/// </summary>
public enum PermissionRole
{
    Owner = 1,
    Admin = 2,
    Member = 3,
    Manager = 4,
    Contributor = 5,
    Viewer = 6,
}
