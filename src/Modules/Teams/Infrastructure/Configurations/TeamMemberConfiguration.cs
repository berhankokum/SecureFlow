using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Teams.Domain;

namespace SecureFlow.Modules.Teams.Infrastructure.Configurations;

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        builder.HasKey(member => new { member.TeamId, member.UserId });
        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_TeamMembers_UserId");
    }
}
