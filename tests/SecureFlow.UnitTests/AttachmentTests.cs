using System.Text;
using SecureFlow.Modules.Files.Application;
using SecureFlow.Modules.Files.Domain;

namespace SecureFlow.UnitTests;

public sealed class AttachmentTests
{
    [Theory]
    [InlineData("image.png", "image/png", "89504E470D0A1A0A")]
    [InlineData("photo.jpg", "image/jpeg", "FFD8FF")]
    [InlineData("PHOTO.JPEG", "IMAGE/JPEG", "FFD8FF")]
    [InlineData("report.PDF", "APPLICATION/PDF", "255044462D")]
    public async Task Every_allowed_type_accepts_matching_case_insensitive_metadata(string name, string mime, string hex)
    {
        var bytes = Convert.FromHexString(hex);
        using var stream = new MemoryStream(bytes);
        var result = await AttachmentContentValidator.ValidateAsync(name, mime, bytes.Length, stream, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Content.Position);
        Assert.Equal(bytes, result.Value.Content.ToArray());
        await result.Value.Content.DisposeAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10485761)]
    public async Task Invalid_declared_size_is_rejected_without_reading_input(long length)
    {
        using var unreadable = new MemoryStream();
        unreadable.Dispose();
        var result = await AttachmentContentValidator.ValidateAsync("x.pdf", "application/pdf", length, unreadable, CancellationToken.None);
        Assert.Equal(length == 0 ? FileErrors.Empty : FileErrors.TooLarge, result.Error);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(20)]
    public async Task Declared_length_cannot_truncate_or_pad_actual_content(long declared)
    {
        using var stream = new MemoryStream("%PDF-1.7"u8.ToArray());
        var result = await AttachmentContentValidator.ValidateAsync("x.pdf", "application/pdf", declared, stream, CancellationToken.None);
        Assert.Equal(FileErrors.SignatureMismatch, result.Error);
    }

    [Fact]
    public async Task Actual_stream_size_is_bounded_even_when_declared_size_is_small()
    {
        using var stream = new MemoryStream(new byte[AttachmentContentValidator.MaximumFileSize + 1]);
        var result = await AttachmentContentValidator.ValidateAsync("x.pdf", "application/pdf", 5, stream, CancellationToken.None);
        Assert.Equal(FileErrors.TooLarge, result.Error);
    }

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
