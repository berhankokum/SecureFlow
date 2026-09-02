using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Projects.Domain;

namespace SecureFlow.Modules.Projects.Infrastructure.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects", table => table.HasCheckConstraint(
            "CK_Projects_Key_Format",
            "\"Key\" ~ '^[A-Z][A-Z0-9]{1,9}$'"));
        builder.HasKey(project => project.Id);
        builder.Ignore(project => project.DomainEvents);

        builder.Property(project => project.Key)
            .HasMaxLength(Project.KeyMaxLength)
            .IsRequired();
        builder.Property(project => project.Name)
            .HasMaxLength(Project.NameMaxLength)
            .IsRequired();
        builder.Property(project => project.Description)
            .HasMaxLength(Project.DescriptionMaxLength);

        builder.HasIndex(project => project.OrganizationId)
            .HasDatabaseName("IX_Projects_OrganizationId");
        builder.HasIndex(project => new { project.OrganizationId, project.Key })
            .IsUnique()
            .HasDatabaseName("UX_Projects_OrganizationId_Key");

        builder.HasMany(project => project.Members)
            .WithOne()
            .HasForeignKey(member => member.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(project => project.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
