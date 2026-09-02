using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Teams.Domain;

namespace SecureFlow.Modules.Teams.Infrastructure.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(team => team.Id);
        builder.Ignore(team => team.DomainEvents);

        builder.Property(team => team.Name)
            .HasMaxLength(Team.NameMaxLength)
            .IsRequired();

        builder.Property(team => team.Description)
            .HasMaxLength(Team.DescriptionMaxLength);

        builder.HasIndex(team => team.OrganizationId)
            .HasDatabaseName("IX_Teams_OrganizationId");

        builder.HasMany(team => team.Members)
            .WithOne()
            .HasForeignKey(member => member.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(team => team.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
