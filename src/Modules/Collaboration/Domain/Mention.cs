using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Collaboration.Domain;

public sealed class Mention : Entity
{
    private Mention()
    {
    }

    private Mention(
        Guid id,
        Guid organizationId,
        Guid commentId,
        Guid userId,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = RequireId(organizationId, nameof(organizationId));
        CommentId = RequireId(commentId, nameof(commentId));
        UserId = RequireId(userId, nameof(userId));
        CreatedAt = createdAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid CommentId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Mention Create(
        Guid organizationId,
        Guid commentId,
        Guid userId,
        DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), organizationId, commentId, userId, createdAt);

    private static Guid RequireId(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException("Id cannot be empty.", parameterName);
}
