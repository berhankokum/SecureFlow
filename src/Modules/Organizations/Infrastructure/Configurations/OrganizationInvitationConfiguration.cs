using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Infrastructure.Configurations;

public sealed class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
    {
        builder.ToTable("OrganizationInvitations", table =>
        {
            table.HasCheckConstraint(
                "CK_OrganizationInvitations_TerminalState",
                "((\"AcceptedAt\" IS NOT NULL)::int + (\"RevokedAt\" IS NOT NULL)::int + "
                + "(\"ExpiredAt\" IS NOT NULL)::int) <= 1");
            table.HasCheckConstraint(
                "CK_OrganizationInvitations_ExpiredAt",
                "\"ExpiredAt\" IS NULL OR \"ExpiredAt\" >= \"ExpiresAt\"");
        });
        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(invitation => invitation.AcceptedAt)
            .IsConcurrencyToken();

        builder.Property(invitation => invitation.RevokedAt)
            .IsConcurrencyToken();

        builder.Property(invitation => invitation.ExpiredAt)
            .IsConcurrencyToken();

        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_OrganizationInvitations_TokenHash");

        // Aynı organization/email için yalnız bir aktif pending invitation olabilir.
        builder.HasIndex(invitation => new { invitation.OrganizationId, invitation.Email })
            .IsUnique()
            .HasFilter("\"AcceptedAt\" IS NULL AND \"RevokedAt\" IS NULL AND \"ExpiredAt\" IS NULL")
            .HasDatabaseName("UX_OrganizationInvitations_PendingEmail");

        builder.HasIndex(invitation => new { invitation.OrganizationId, invitation.CreatedAt })
            .HasDatabaseName("IX_OrganizationInvitations_OrganizationId_CreatedAt");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(invitation => invitation.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
