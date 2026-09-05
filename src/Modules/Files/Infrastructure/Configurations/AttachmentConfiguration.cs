using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Files.Domain;

namespace SecureFlow.Modules.Files.Infrastructure.Configurations;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments", table =>
            table.HasCheckConstraint("CK_Attachments_Size_Positive", "\"Size\" > 0"));
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.OriginalFileName)
            .HasMaxLength(Attachment.OriginalFileNameMaxLength)
            .IsRequired();
        builder.Property(attachment => attachment.StorageKey)
            .HasMaxLength(Attachment.StorageKeyMaxLength)
            .IsRequired();
        builder.Property(attachment => attachment.ContentType)
            .HasMaxLength(Attachment.ContentTypeMaxLength)
            .IsRequired();
        builder.HasIndex(attachment => attachment.StorageKey)
            .IsUnique()
            .HasDatabaseName("UX_Attachments_StorageKey");
        builder.HasIndex(attachment => new { attachment.WorkItemId, attachment.UploadedAt })
            .HasDatabaseName("IX_Attachments_WorkItemId_UploadedAt");
        builder.HasIndex(attachment => new { attachment.OrganizationId, attachment.UploadedBy })
            .HasDatabaseName("IX_Attachments_OrganizationId_UploadedBy");
    }
}
