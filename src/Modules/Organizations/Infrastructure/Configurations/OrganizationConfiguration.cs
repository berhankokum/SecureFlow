using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Infrastructure.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(organization => organization.Id);
        builder.Ignore(organization => organization.DomainEvents);

        builder.Property(organization => organization.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(organization => organization.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(organization => organization.LogoUrl)
            .HasMaxLength(2048);

        builder.HasIndex(organization => organization.Slug)
            .IsUnique()
            .HasDatabaseName("UX_Organizations_Slug");

        builder.HasMany(organization => organization.Members)
            .WithOne()
            .HasForeignKey(member => member.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(organization => organization.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
