using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Infrastructure.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMembers");
        builder.HasKey(member => member.Id);

        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(member => new { member.OrganizationId, member.UserId })
            .IsUnique()
            .HasDatabaseName("UX_OrganizationMembers_OrganizationId_UserId");
    }
}
