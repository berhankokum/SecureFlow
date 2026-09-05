using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Files.Domain;

public sealed class Attachment : Entity
{
    public const int OriginalFileNameMaxLength = 255;
    public const int StorageKeyMaxLength = 160;
    public const int ContentTypeMaxLength = 100;

    private Attachment()
    {
    }

    private Attachment(
        Guid id,
        Guid organizationId,
        Guid workItemId,
        string originalFileName,
        string storageKey,
        string contentType,
        long size,
        Guid uploadedBy,
        DateTimeOffset uploadedAt)
        : base(id)
    {
        OrganizationId = RequireId(organizationId, nameof(organizationId));
        WorkItemId = RequireId(workItemId, nameof(workItemId));
        OriginalFileName = RequireText(originalFileName, OriginalFileNameMaxLength, nameof(originalFileName));
        StorageKey = RequireText(storageKey, StorageKeyMaxLength, nameof(storageKey));
        ContentType = RequireText(contentType, ContentTypeMaxLength, nameof(contentType));
        Size = size > 0 ? size : throw new ArgumentOutOfRangeException(nameof(size));
        UploadedBy = RequireId(uploadedBy, nameof(uploadedBy));
        UploadedAt = uploadedAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid WorkItemId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public Guid UploadedBy { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public static Attachment Create(
        Guid organizationId,
        Guid workItemId,
        string originalFileName,
        string contentType,
        long size,
        Guid uploadedBy,
        DateTimeOffset uploadedAt)
    {
        var id = Guid.NewGuid();
        return new Attachment(
            id,
            organizationId,
            workItemId,
            originalFileName,
            $"attachments/{organizationId:N}/{id:N}",
            contentType,
            size,
            uploadedBy,
            uploadedAt);
    }

    public void Delete(DateTimeOffset deletedAt)
    {
        DeletedAt ??= deletedAt;
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException("Id cannot be empty.", parameterName);

    private static string RequireText(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        }

        return value;
    }
}
