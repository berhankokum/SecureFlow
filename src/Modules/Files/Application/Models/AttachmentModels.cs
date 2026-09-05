namespace SecureFlow.Modules.Files.Application.Models;

public sealed record AttachmentSummary(
    Guid Id,
    Guid OrganizationId,
    Guid WorkItemId,
    string OriginalFileName,
    string ContentType,
    long Size,
    Guid UploadedBy,
    string UploadedByEmail,
    DateTimeOffset UploadedAt,
    bool CanDelete);

public sealed record AttachmentDownload(
    Stream Content,
    string ContentType,
    long Length,
    string FileName);
