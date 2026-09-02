using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Infrastructure.Configurations;

public sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems", table =>
        {
            table.HasCheckConstraint("CK_WorkItems_Number_Positive", "\"Number\" > 0");
            table.HasCheckConstraint("CK_WorkItems_Type", "\"Type\" IN ('TASK', 'BUG', 'STORY')");
            table.HasCheckConstraint(
                "CK_WorkItems_Priority",
                "\"Priority\" IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')");
        });
        builder.HasKey(workItem => workItem.Id);
        builder.Ignore(workItem => workItem.DomainEvents);

        builder.Property(workItem => workItem.Type)
            .HasConversion(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<WorkItemType>(value, true))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(workItem => workItem.Title)
            .HasMaxLength(WorkItem.TitleMaxLength)
            .IsRequired();
        builder.Property(workItem => workItem.Description)
            .HasMaxLength(WorkItem.DescriptionMaxLength);
        builder.Property(workItem => workItem.Priority)
            .HasConversion(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<WorkItemPriority>(value, true))
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(workItem => workItem.ProjectId)
            .HasDatabaseName("IX_WorkItems_ProjectId");
        builder.HasIndex(workItem => new { workItem.ProjectId, workItem.Number })
            .IsUnique()
            .HasDatabaseName("UX_WorkItems_ProjectId_Number");
        builder.HasIndex(workItem => new { workItem.ProjectId, workItem.DueDate })
            .HasDatabaseName("IX_WorkItems_ProjectId_DueDate");
    }
}
