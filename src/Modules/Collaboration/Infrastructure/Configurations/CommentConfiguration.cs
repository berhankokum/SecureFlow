using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Collaboration.Domain;

namespace SecureFlow.Modules.Collaboration.Infrastructure.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments", table => table.HasCheckConstraint(
            "CK_Comments_Content_NotBlank",
            "length(btrim(\"Content\")) > 0"));
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Content)
            .HasMaxLength(Comment.ContentMaxLength)
            .IsRequired();
        builder.HasIndex(comment => new { comment.WorkItemId, comment.CreatedAt })
            .HasDatabaseName("IX_Comments_WorkItemId_CreatedAt");
        builder.HasIndex(comment => new { comment.OrganizationId, comment.Id })
            .IsUnique()
            .HasDatabaseName("UX_Comments_OrganizationId_Id");
    }
}
