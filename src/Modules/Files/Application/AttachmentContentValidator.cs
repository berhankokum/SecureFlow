using SecureFlow.Modules.Files.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Files.Application;

public sealed record ValidatedAttachmentContent(
    string OriginalFileName,
    string ContentType,
    long Size,
    MemoryStream Content);

public static class AttachmentContentValidator
{
    public const long MaximumFileSize = 10L * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, (string ContentType, byte[] Signature)> Allowed =
        new Dictionary<string, (string, byte[])>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("application/pdf", "%PDF-"u8.ToArray()),
            [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
            [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        };

    public static async Task<Result<ValidatedAttachmentContent>> ValidateAsync(
        string fileName,
        string contentType,
        long declaredLength,
        Stream source,
        CancellationToken cancellationToken)
    {
        if (!IsValidFileName(fileName))
        {
            return Result.Failure<ValidatedAttachmentContent>(FileErrors.InvalidFileName);
        }

        if (declaredLength <= 0)
        {
            return Result.Failure<ValidatedAttachmentContent>(FileErrors.Empty);
        }

        if (declaredLength > MaximumFileSize)
        {
            return Result.Failure<ValidatedAttachmentContent>(FileErrors.TooLarge);
        }

        var extension = Path.GetExtension(fileName);
        if (!Allowed.TryGetValue(extension, out var allowed)
            || !string.Equals(contentType, allowed.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ValidatedAttachmentContent>(FileErrors.UnsupportedType);
        }

        var buffer = new MemoryStream((int)declaredLength);
        var copyBuffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(copyBuffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > MaximumFileSize)
            {
                await buffer.DisposeAsync();
                return Result.Failure<ValidatedAttachmentContent>(FileErrors.TooLarge);
            }

            await buffer.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            await buffer.DisposeAsync();
            return Result.Failure<ValidatedAttachmentContent>(FileErrors.Empty);
        }

        if (total != declaredLength || total < allowed.Signature.Length)
        {
            await buffer.DisposeAsync();
            return Result.Failure<ValidatedAttachmentContent>(FileErrors.SignatureMismatch);
        }

        buffer.Position = 0;
        var signature = new byte[allowed.Signature.Length];
        _ = await buffer.ReadAsync(signature, cancellationToken);
        if (!signature.AsSpan().SequenceEqual(allowed.Signature))
        {
            await buffer.DisposeAsync();
            return Result.Failure<ValidatedAttachmentContent>(FileErrors.SignatureMismatch);
        }

        buffer.Position = 0;
        return Result.Success(new ValidatedAttachmentContent(
            fileName,
            allowed.ContentType,
            total,
            buffer));
    }

    private static bool IsValidFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= Attachment.OriginalFileNameMaxLength
        && fileName == fileName.Trim()
        && !fileName.Contains('/')
        && !fileName.Contains('\\')
        && !fileName.Any(char.IsControl)
        && !string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fileName));
}
