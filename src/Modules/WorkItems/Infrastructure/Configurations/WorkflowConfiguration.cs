using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Infrastructure.Configurations;

public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("Workflows");
        builder.HasKey(workflow => workflow.Id);
        builder.Ignore(workflow => workflow.DomainEvents);
        builder.Property(workflow => workflow.Name).HasMaxLength(Workflow.NameMaxLength).IsRequired();
        builder.HasIndex(workflow => workflow.OrganizationId)
            .IsUnique()
            .HasDatabaseName("UX_Workflows_OrganizationId");

        builder.HasMany(workflow => workflow.States)
            .WithOne()
            .HasForeignKey(state => state.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(workflow => workflow.States).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(workflow => workflow.Transitions)
            .WithOne()
            .HasForeignKey(transition => transition.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(workflow => workflow.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
