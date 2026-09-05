using System.Text;
using SecureFlow.Modules.Files.Application;
using SecureFlow.Modules.Files.Domain;

namespace SecureFlow.UnitTests;

public sealed class AttachmentTests
{
    [Fact]
    public void Create_generates_tenant_scoped_storage_key_without_original_name()
    {
        var organizationId = Guid.NewGuid();
        var attachment = Attachment.Create(
            organizationId,
            Guid.NewGuid(),
            "sensitive-name.pdf",
            "application/pdf",
            42,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.StartsWith($"attachments/{organizationId:N}/", attachment.StorageKey, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-name", attachment.StorageKey, StringComparison.Ordinal);
        Assert.False(attachment.IsDeleted);
    }

    [Fact]
    public async Task Validator_accepts_only_matching_extension_mime_and_magic_bytes()
    {
        var validBytes = Encoding.ASCII.GetBytes("%PDF-1.7\ncontent");
        await using var validStream = new MemoryStream(validBytes);
        var valid = await AttachmentContentValidator.ValidateAsync(
            "report.pdf",
            "application/pdf",
            validBytes.Length,
            validStream,
            CancellationToken.None);
        Assert.True(valid.IsSuccess);
        await valid.Value.Content.DisposeAsync();

        await using var invalidStream = new MemoryStream(Encoding.ASCII.GetBytes("not-pdf"));
        var invalid = await AttachmentContentValidator.ValidateAsync(
            "report.pdf",
            "application/pdf",
            invalidStream.Length,
            invalidStream,
            CancellationToken.None);
        Assert.True(invalid.IsFailure);
        Assert.Equal(FileErrors.SignatureMismatch, invalid.Error);
    }

    [Theory]
    [InlineData("../report.pdf")]
    [InlineData("folder\\report.pdf")]
    [InlineData(" report.pdf")]
    [InlineData("report\u0000.pdf")]
    public async Task Validator_rejects_unsafe_original_names(string fileName)
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7");
        await using var stream = new MemoryStream(bytes);
        var result = await AttachmentContentValidator.ValidateAsync(
            fileName,
            "application/pdf",
            bytes.Length,
            stream,
            CancellationToken.None);
        Assert.Equal(FileErrors.InvalidFileName, result.Error);
    }
}
