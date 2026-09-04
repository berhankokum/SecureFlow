using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Infrastructure.Configurations;

public sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions", table =>
            table.HasCheckConstraint(
                "CK_WorkflowTransitions_DifferentStates",
                "\"FromStateId\" <> \"ToStateId\""));
        builder.HasKey(transition => transition.Id);
        builder.Property(transition => transition.RequiredPermission)
            .HasMaxLength(WorkflowTransition.RequiredPermissionMaxLength);
        builder.HasIndex(transition => new
            {
                transition.WorkflowId,
                transition.FromStateId,
                transition.ToStateId,
            })
            .IsUnique()
            .HasDatabaseName("UX_WorkflowTransitions_WorkflowId_FromStateId_ToStateId");

        builder.HasOne<WorkflowState>()
            .WithMany()
            .HasForeignKey(transition => new { transition.WorkflowId, transition.FromStateId })
            .HasPrincipalKey(state => new { state.WorkflowId, state.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowState>()
            .WithMany()
            .HasForeignKey(transition => new { transition.WorkflowId, transition.ToStateId })
            .HasPrincipalKey(state => new { state.WorkflowId, state.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
