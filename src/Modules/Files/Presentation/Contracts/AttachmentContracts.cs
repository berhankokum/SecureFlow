using SecureFlow.Modules.Files.Application.Models;

namespace SecureFlow.Modules.Files.Presentation.Contracts;

public sealed record AttachmentResponse(
    Guid Id,
    Guid OrganizationId,
    Guid WorkItemId,
    string OriginalFileName,
    string ContentType,
    long Size,
    Guid UploadedBy,
    string UploadedByEmail,
    DateTimeOffset UploadedAt,
    bool CanDelete)
{
    public static AttachmentResponse From(AttachmentSummary attachment) => new(
        attachment.Id,
        attachment.OrganizationId,
        attachment.WorkItemId,
        attachment.OriginalFileName,
        attachment.ContentType,
        attachment.Size,
        attachment.UploadedBy,
        attachment.UploadedByEmail,
        attachment.UploadedAt,
        attachment.CanDelete);
}
