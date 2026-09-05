using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureFlow.Contracts.Activity;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Identity;
using SecureFlow.Modules.Files.Application.Abstractions;
using SecureFlow.Modules.Files.Application.Models;
using SecureFlow.Modules.Files.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Files.Application;

public sealed class FileScopeHandler(IFilesDbContext dbContext)
{
    public Task<FileResourceScope?> FindWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken) =>
        dbContext.FindWorkItemFileScopeAsync(workItemId, cancellationToken);

    public async Task<FileResourceScope?> FindAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var workItemId = await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.Id == attachmentId && attachment.DeletedAt == null)
            .Select(attachment => (Guid?)attachment.WorkItemId)
            .SingleOrDefaultAsync(cancellationToken);
        return workItemId is null
            ? null
            : await dbContext.FindWorkItemFileScopeAsync(workItemId.Value, cancellationToken);
    }
}

public sealed class AttachmentHandlers(
    IFilesDbContext dbContext,
    IObjectStorage objectStorage,
    IUserDirectory userDirectory,
    IActivityWriter activityWriter,
    IClock clock,
    ILogger<AttachmentHandlers> logger)
{
    public async Task<Result<IReadOnlyList<AttachmentSummary>>> ListAsync(
        Guid workItemId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var access = await dbContext.GetFileAccessAsync(workItemId, currentUserId, cancellationToken);
        if (access?.HasProjectAccess != true)
        {
            return Result.Failure<IReadOnlyList<AttachmentSummary>>(FileErrors.NotFound);
        }

        var attachments = await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.WorkItemId == workItemId
                && attachment.OrganizationId == access.OrganizationId
                && attachment.DeletedAt == null)
            .OrderByDescending(attachment => attachment.UploadedAt)
            .ThenByDescending(attachment => attachment.Id)
            .ToArrayAsync(cancellationToken);
        var emails = await userDirectory.GetEmailsByUserIdsAsync(
            attachments.Select(attachment => attachment.UploadedBy).Distinct().ToArray(),
            cancellationToken);
        return Result.Success<IReadOnlyList<AttachmentSummary>>(
            attachments.Select(attachment => ToSummary(attachment, access, currentUserId, emails)).ToArray());
    }

    public async Task<Result<AttachmentSummary>> UploadAsync(
        Guid workItemId,
        Guid currentUserId,
        string fileName,
        string contentType,
        long length,
        Stream content,
        CancellationToken cancellationToken)
    {
        var validation = await AttachmentContentValidator.ValidateAsync(
            fileName,
            contentType,
            length,
            content,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<AttachmentSummary>(validation.Error);
        }

        await using var validatedContent = validation.Value.Content;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var accessResult = await RequireMutableAccessAsync(
            workItemId,
            currentUserId,
            Permissions.AttachmentUpload,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result.Failure<AttachmentSummary>(accessResult.Error);
        }

        var now = clock.UtcNow;
        var attachment = Attachment.Create(
            accessResult.Value.OrganizationId,
            workItemId,
            validation.Value.OriginalFileName,
            validation.Value.ContentType,
            validation.Value.Size,
            currentUserId,
            now);
        try
        {
            await objectStorage.PutAsync(
                attachment.StorageKey,
                validatedContent,
                attachment.ContentType,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Attachment object upload failed for {AttachmentId}.", attachment.Id);
            return Result.Failure<AttachmentSummary>(FileErrors.StorageUnavailable);
        }

        try
        {
            dbContext.Attachments.Add(attachment);
            activityWriter.Add(new ActivityWriteRequest(
                attachment.OrganizationId,
                accessResult.Value.ProjectId,
                attachment.WorkItemId,
                currentUserId,
                ActivityTypes.AttachmentUploaded,
                new Dictionary<string, string?>
                {
                    ["attachmentId"] = attachment.Id.ToString(),
                    ["fileName"] = attachment.OriginalFileName,
                },
                now));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await TryCompensatingDeleteAsync(attachment.StorageKey, attachment.Id);
            throw;
        }

        var uploader = await userDirectory.FindByIdAsync(currentUserId, cancellationToken);
        return Result.Success(ToSummary(
            attachment,
            accessResult.Value,
            currentUserId,
            new Dictionary<Guid, string>
            {
                [currentUserId] = uploader?.Email ?? "Bilinmeyen kullanıcı",
            }));
    }

    public async Task<Result<AttachmentDownload>> DownloadAsync(
        Guid attachmentId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == attachmentId && item.DeletedAt == null,
                cancellationToken);
        if (attachment is null)
        {
            return Result.Failure<AttachmentDownload>(FileErrors.NotFound);
        }

        var access = await dbContext.GetFileAccessAsync(
            attachment.WorkItemId,
            currentUserId,
            cancellationToken);
        if (access?.HasProjectAccess != true || access.OrganizationId != attachment.OrganizationId)
        {
            return Result.Failure<AttachmentDownload>(FileErrors.NotFound);
        }

        try
        {
            var storedObject = await objectStorage.GetAsync(attachment.StorageKey, cancellationToken);
            return storedObject is null
                ? Result.Failure<AttachmentDownload>(FileErrors.StorageUnavailable)
                : Result.Success(new AttachmentDownload(
                    storedObject.Content,
                    attachment.ContentType,
                    attachment.Size,
                    attachment.OriginalFileName));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Attachment object download failed for {AttachmentId}.", attachment.Id);
            return Result.Failure<AttachmentDownload>(FileErrors.StorageUnavailable);
        }
    }

    public async Task<Result> DeleteAsync(
        Guid attachmentId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var attachmentScope = await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.Id == attachmentId && attachment.DeletedAt == null)
            .Select(attachment => new { attachment.WorkItemId, attachment.OrganizationId })
            .SingleOrDefaultAsync(cancellationToken);
        if (attachmentScope is null)
        {
            return Result.Failure(FileErrors.NotFound);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var accessResult = await RequireMutableAccessAsync(
            attachmentScope.WorkItemId,
            currentUserId,
            Permissions.AttachmentDelete,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result.Failure(accessResult.Error);
        }

        var attachment = await dbContext.Attachments.SingleOrDefaultAsync(
            item => item.Id == attachmentId
                && item.WorkItemId == accessResult.Value.WorkItemId
                && item.OrganizationId == accessResult.Value.OrganizationId
                && item.DeletedAt == null,
            cancellationToken);
        if (attachment is null)
        {
            return Result.Failure(FileErrors.NotFound);
        }

        if (attachment.UploadedBy != currentUserId && !accessResult.Value.CanDeleteAnyAttachment)
        {
            return Result.Failure(FileErrors.Forbidden);
        }

        attachment.Delete(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        try
        {
            await objectStorage.DeleteAsync(attachment.StorageKey, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Soft-deleted attachment object cleanup failed for {AttachmentId}; durable retry is deferred to Phase 17.",
                attachment.Id);
        }

        return Result.Success();
    }

    private async Task<Result<FileAccessContext>> RequireMutableAccessAsync(
        Guid workItemId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        var resource = await dbContext.FindWorkItemFileScopeAsync(workItemId, cancellationToken);
        if (resource is null || !await dbContext.LockProjectAsync(resource.ProjectId, cancellationToken))
        {
            return Result.Failure<FileAccessContext>(FileErrors.NotFound);
        }

        if (!await dbContext.LockWorkItemAsync(workItemId, cancellationToken))
        {
            return Result.Failure<FileAccessContext>(FileErrors.NotFound);
        }

        var access = await dbContext.GetFileAccessAsync(workItemId, userId, cancellationToken);
        if (access?.HasProjectAccess != true)
        {
            return Result.Failure<FileAccessContext>(FileErrors.NotFound);
        }

        if (!access.Permissions.Contains(permission, StringComparer.Ordinal))
        {
            return Result.Failure<FileAccessContext>(FileErrors.Forbidden);
        }

        if (access.IsProjectArchived)
        {
            return Result.Failure<FileAccessContext>(FileErrors.ProjectArchived);
        }

        return access.IsWorkItemArchived
            ? Result.Failure<FileAccessContext>(FileErrors.WorkItemArchived)
            : Result.Success(access);
    }

    private async Task TryCompensatingDeleteAsync(string storageKey, Guid attachmentId)
    {
        try
        {
            await objectStorage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Attachment upload compensation failed for {AttachmentId}.", attachmentId);
        }
    }

    private static AttachmentSummary ToSummary(
        Attachment attachment,
        FileAccessContext access,
        Guid currentUserId,
        IReadOnlyDictionary<Guid, string> emails)
    {
        var canDelete = !access.IsProjectArchived
            && !access.IsWorkItemArchived
            && access.Permissions.Contains(Permissions.AttachmentDelete, StringComparer.Ordinal)
            && (attachment.UploadedBy == currentUserId || access.CanDeleteAnyAttachment);
        return new AttachmentSummary(
            attachment.Id,
            attachment.OrganizationId,
            attachment.WorkItemId,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.Size,
            attachment.UploadedBy,
            emails.GetValueOrDefault(attachment.UploadedBy, "Bilinmeyen kullanıcı"),
            attachment.UploadedAt,
            canDelete);
    }
}
