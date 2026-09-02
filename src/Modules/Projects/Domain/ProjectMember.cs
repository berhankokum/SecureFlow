using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Projects.Domain;

public sealed class ProjectMember
{
    private ProjectMember()
    {
    }

    private ProjectMember(Guid projectId, Guid userId, ProjectRole role)
    {
        ProjectId = projectId;
        UserId = userId;
        Role = role;
    }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public ProjectRole Role { get; private set; }

    public static ProjectMember Create(Guid projectId, Guid userId, ProjectRole role)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id cannot be empty.", nameof(projectId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        return new ProjectMember(projectId, userId, role);
    }

    public void ChangeRole(ProjectRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("Geçersiz Project role.");
        }

        Role = role;
    }
}
