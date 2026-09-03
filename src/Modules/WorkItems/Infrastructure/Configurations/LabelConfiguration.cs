using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Infrastructure.Configurations;

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("Labels");
        builder.HasKey(label => label.Id);
        builder.Ignore(label => label.DomainEvents);
        builder.Property(label => label.Name).HasMaxLength(Label.NameMaxLength).IsRequired();
        builder.Property(label => label.NormalizedName).HasMaxLength(Label.NameMaxLength).IsRequired();
        builder.HasIndex(label => new { label.ProjectId, label.NormalizedName })
            .IsUnique().HasDatabaseName("UX_Labels_ProjectId_NormalizedName");
    }
}

public sealed class WorkItemLabelConfiguration : IEntityTypeConfiguration<WorkItemLabel>
{
    public void Configure(EntityTypeBuilder<WorkItemLabel> builder)
    {
        builder.ToTable("WorkItemLabels");
        builder.HasKey(assignment => new { assignment.WorkItemId, assignment.LabelId });
        builder.HasIndex(assignment => new { assignment.LabelId, assignment.WorkItemId })
            .HasDatabaseName("IX_WorkItemLabels_LabelId_WorkItemId");
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(assignment => assignment.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Label>().WithMany().HasForeignKey(assignment => assignment.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
