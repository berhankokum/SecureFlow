using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Infrastructure.Configurations;

public sealed class WorkflowStateConfiguration : IEntityTypeConfiguration<WorkflowState>
{
    public void Configure(EntityTypeBuilder<WorkflowState> builder)
    {
        builder.ToTable("WorkflowStates", table =>
        {
            table.HasCheckConstraint("CK_WorkflowStates_Position", "\"Position\" >= 0");
            table.HasCheckConstraint(
                "CK_WorkflowStates_Category",
                "\"Category\" IN ('TODO', 'IN_PROGRESS', 'DONE')");
        });
        builder.HasKey(state => state.Id);
        builder.HasAlternateKey(state => new { state.WorkflowId, state.Id })
            .HasName("AK_WorkflowStates_WorkflowId_Id");
        builder.Property(state => state.Name).HasMaxLength(WorkflowState.NameMaxLength).IsRequired();
        builder.Property(state => state.Category)
            .HasConversion(
                value => value == WorkflowCategory.Todo
                    ? "TODO"
                    : value == WorkflowCategory.InProgress ? "IN_PROGRESS" : "DONE",
                value => value == "TODO"
                    ? WorkflowCategory.Todo
                    : value == "IN_PROGRESS" ? WorkflowCategory.InProgress : WorkflowCategory.Done)
            .HasMaxLength(16)
            .IsRequired();
        builder.HasIndex(state => new { state.WorkflowId, state.Position })
            .IsUnique()
            .HasDatabaseName("UX_WorkflowStates_WorkflowId_Position");
    }
}
