using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
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
        builder.Property(workItem => workItem.Version).IsRowVersion();
        builder.Ignore(workItem => workItem.DomainEvents);
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasComputedColumnSql("to_tsvector('simple'::regconfig, coalesce(\"Title\", '') || ' ' || coalesce(\"Description\", ''))", stored: true);
        builder.HasIndex("SearchVector").HasMethod("GIN");

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

        builder.HasOne<WorkflowState>()
            .WithMany()
            .HasForeignKey(workItem => workItem.WorkflowStateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workItem => workItem.ProjectId)
            .HasDatabaseName("IX_WorkItems_ProjectId");
        builder.HasIndex(workItem => new { workItem.ProjectId, workItem.Number })
            .IsUnique()
            .HasDatabaseName("UX_WorkItems_ProjectId_Number");
        builder.HasIndex(workItem => new { workItem.ProjectId, workItem.DueDate })
            .HasDatabaseName("IX_WorkItems_ProjectId_DueDate");
        builder.HasIndex(workItem => workItem.DueDate)
            .HasFilter("\"DueDate\" IS NOT NULL AND \"ArchivedAt\" IS NULL")
            .HasDatabaseName("IX_WorkItems_DueDate_Active");
        builder.HasIndex(workItem => new { workItem.ProjectId, workItem.AssigneeId })
            .HasDatabaseName("IX_WorkItems_ProjectId_AssigneeId");
        builder.HasIndex(workItem => new { workItem.ProjectId, workItem.WorkflowStateId })
            .HasDatabaseName("IX_WorkItems_ProjectId_WorkflowStateId");
    }
}
