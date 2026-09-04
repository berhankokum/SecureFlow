using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Collaboration.Domain;

namespace SecureFlow.Modules.Collaboration.Infrastructure.Configurations;

public sealed class MentionConfiguration : IEntityTypeConfiguration<Mention>
{
    public void Configure(EntityTypeBuilder<Mention> builder)
    {
        builder.ToTable("Mentions");
        builder.HasKey(mention => mention.Id);
        builder.HasIndex(mention => new { mention.CommentId, mention.UserId })
            .IsUnique()
            .HasDatabaseName("UX_Mentions_CommentId_UserId");
        builder.HasIndex(mention => new { mention.UserId, mention.CreatedAt })
            .HasDatabaseName("IX_Mentions_UserId_CreatedAt");
    }
}
