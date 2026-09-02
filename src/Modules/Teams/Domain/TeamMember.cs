namespace SecureFlow.Modules.Teams.Domain;

public sealed class TeamMember
{
    private TeamMember()
    {
    }

    private TeamMember(Guid teamId, Guid userId, DateTimeOffset addedAt, Guid addedBy)
    {
        TeamId = teamId;
        UserId = userId;
        AddedAt = addedAt;
        AddedBy = addedBy;
    }

    public Guid TeamId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public Guid AddedBy { get; private set; }

    public static TeamMember Create(Guid teamId, Guid userId, Guid addedBy, DateTimeOffset addedAt)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Team id cannot be empty.", nameof(teamId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        if (addedBy == Guid.Empty)
        {
            throw new ArgumentException("Actor user id cannot be empty.", nameof(addedBy));
        }

        return new TeamMember(teamId, userId, addedAt, addedBy);
    }
}
