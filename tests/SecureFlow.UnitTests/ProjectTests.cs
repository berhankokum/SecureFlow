using SecureFlow.Modules.Projects.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.UnitTests;

public class ProjectTests
{
    [Fact]
    public void Create_normalizes_key_and_adds_creator_as_manager()
    {
        var creatorId = Guid.NewGuid();
        var project = Project.Create(
            Guid.NewGuid(),
            " sf1 ",
            "  SecureFlow  ",
            "  Delivery platform  ",
            creatorId,
            DateTimeOffset.UtcNow);

        Assert.Equal("SF1", project.Key);
        Assert.Equal("SecureFlow", project.Name);
        Assert.Equal("Delivery platform", project.Description);
        var creator = Assert.Single(project.Members);
        Assert.Equal(creatorId, creator.UserId);
        Assert.Equal(ProjectRole.Manager, creator.Role);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("1A")]
    [InlineData("A-1")]
    [InlineData("ABCDEFGHIJK")]
    public void Create_rejects_invalid_project_keys(string key)
    {
        Assert.Throws<ArgumentException>(() => Project.Create(
            Guid.NewGuid(), key, "Valid", null, Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Archived_project_is_read_only_and_preserves_members()
    {
        var project = Project.Create(
            Guid.NewGuid(), "SF", "SecureFlow", null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        project.AddMember(userId, ProjectRole.Contributor);
        var archivedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        project.Archive(archivedAt);
        project.Archive(archivedAt.AddMinutes(1));

        Assert.Equal(archivedAt, project.ArchivedAt);
        Assert.Equal(2, project.Members.Count);
        Assert.Throws<DomainException>(() => project.Update("Other", null, DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => project.AddMember(Guid.NewGuid(), ProjectRole.Viewer));
        Assert.Throws<DomainException>(() => project.ChangeMemberRole(userId, ProjectRole.Manager));
        Assert.Throws<DomainException>(() => project.RemoveMember(userId));
    }

    [Fact]
    public void Membership_rejects_duplicates_and_supports_explicit_role_changes()
    {
        var project = Project.Create(
            Guid.NewGuid(), "SF", "SecureFlow", null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        project.AddMember(userId, ProjectRole.Viewer);

        Assert.Throws<DomainException>(() => project.AddMember(userId, ProjectRole.Manager));
        Assert.True(project.ChangeMemberRole(userId, ProjectRole.Contributor));
        Assert.Equal(ProjectRole.Contributor, project.Members.Single(member => member.UserId == userId).Role);
        Assert.True(project.RemoveMember(userId));
        Assert.False(project.RemoveMember(userId));
    }
}
