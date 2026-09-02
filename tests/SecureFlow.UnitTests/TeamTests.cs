using SecureFlow.Modules.Teams.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.UnitTests;

public class TeamTests
{
    [Fact]
    public void Create_normalizes_values_and_allows_duplicate_names_at_domain_level()
    {
        var organizationId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);

        var first = Team.Create(organizationId, "  Platform  ", "  Core services  ", createdAt);
        var second = Team.Create(organizationId, "Platform", null, createdAt);

        Assert.Equal("Platform", first.Name);
        Assert.Equal("Core services", first.Description);
        Assert.Equal("Platform", second.Name);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Archive_is_idempotent_and_makes_team_read_only_while_preserving_members()
    {
        var team = Team.Create(Guid.NewGuid(), "Security", null, DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        team.AddMember(userId, actorId, DateTimeOffset.UtcNow);
        var archivedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        team.Archive(archivedAt);
        team.Archive(archivedAt.AddMinutes(1));

        Assert.Equal(archivedAt, team.ArchivedAt);
        Assert.Single(team.Members);
        Assert.Throws<DomainException>(() => team.Update("Other", null, DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => team.AddMember(Guid.NewGuid(), actorId, DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => team.RemoveMember(userId));
    }

    [Fact]
    public void Team_rejects_duplicate_members_and_invalid_lengths()
    {
        var team = Team.Create(Guid.NewGuid(), "Security", null, DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        team.AddMember(userId, actorId, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => team.AddMember(userId, actorId, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Team.Create(
            Guid.NewGuid(), new string('a', Team.NameMaxLength + 1), null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Team.Create(
            Guid.NewGuid(), "Valid", new string('a', Team.DescriptionMaxLength + 1), DateTimeOffset.UtcNow));
    }
}
