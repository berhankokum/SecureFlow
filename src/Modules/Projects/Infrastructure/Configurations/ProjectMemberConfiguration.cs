using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Projects.Domain;

namespace SecureFlow.Modules.Projects.Infrastructure.Configurations;

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers", table => table.HasCheckConstraint(
            "CK_ProjectMembers_ProjectRole",
            "\"ProjectRole\" IN ('Manager', 'Contributor', 'Viewer')"));
        builder.HasKey(member => new { member.ProjectId, member.UserId });
        builder.Property(member => member.Role)
            .HasColumnName("ProjectRole")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_ProjectMembers_UserId");
    }
}
