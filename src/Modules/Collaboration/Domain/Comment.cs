using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Collaboration.Domain;

public sealed class Comment : Entity
{
    public const int ContentMaxLength = 5_000;

    private Comment()
    {
    }

    private Comment(
        Guid id,
        Guid organizationId,
        Guid workItemId,
        Guid authorId,
        string content,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = RequireId(organizationId, nameof(organizationId));
        WorkItemId = RequireId(workItemId, nameof(workItemId));
        AuthorId = RequireId(authorId, nameof(authorId));
        Content = NormalizeContent(content);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid WorkItemId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public static Comment Create(
        Guid organizationId,
        Guid workItemId,
        Guid authorId,
        string content,
        DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), organizationId, workItemId, authorId, content, createdAt);

    public bool Update(string content, DateTimeOffset updatedAt)
    {
        EnsureActive();
        var normalized = NormalizeContent(content);
        if (string.Equals(Content, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        Content = normalized;
        UpdatedAt = updatedAt;
        return true;
    }

    public bool Delete(DateTimeOffset deletedAt)
    {
        if (IsDeleted)
        {
            return false;
        }

        DeletedAt = deletedAt;
        UpdatedAt = deletedAt;
        return true;
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException("Id cannot be empty.", parameterName);

    private static string NormalizeContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var normalized = content.Trim();
        if (normalized.Length > ContentMaxLength)
        {
            throw new ArgumentException(
                $"Content cannot exceed {ContentMaxLength} characters.",
                nameof(content));
        }

        return normalized;
    }

    private void EnsureActive()
    {
        if (IsDeleted)
        {
            throw new DomainException("Silinmiş yorum değiştirilemez.");
        }
    }
}
