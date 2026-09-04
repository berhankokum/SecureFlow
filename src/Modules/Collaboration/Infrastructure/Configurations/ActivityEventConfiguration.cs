using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Collaboration.Domain;

namespace SecureFlow.Modules.Collaboration.Infrastructure.Configurations;

public sealed class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        builder.ToTable("ActivityEvents");
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Type)
            .HasMaxLength(ActivityEvent.TypeMaxLength)
            .IsRequired();
        builder.Property(activity => activity.Metadata)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.HasIndex(activity => new { activity.WorkItemId, activity.CreatedAt })
            .HasDatabaseName("IX_ActivityEvents_WorkItemId_CreatedAt");
        builder.HasIndex(activity => new { activity.ProjectId, activity.CreatedAt })
            .HasDatabaseName("IX_ActivityEvents_ProjectId_CreatedAt");
    }
}
