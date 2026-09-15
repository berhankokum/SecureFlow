using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Audit.Domain;

namespace SecureFlow.Modules.Audit.Infrastructure;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", table =>
        {
            table.HasCheckConstraint("CK_AuditLogs_Scope", "(\"OrganizationId\" IS NULL) = (\"Action\" IN ('LOGIN_FAILURE', 'PASSWORD_CHANGED'))");
            table.HasCheckConstraint("CK_AuditLogs_Metadata", "jsonb_typeof(\"Metadata\") = 'object'");
        });
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Action).HasMaxLength(64).IsRequired();
        builder.Property(log => log.ResourceType).HasMaxLength(64).IsRequired();
        builder.Property(log => log.IpAddress).HasMaxLength(45);
        builder.Property(log => log.UserAgent).HasMaxLength(512);
        builder.Property(log => log.Metadata).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(log => new { log.OrganizationId, log.Timestamp, log.Id });
    }
}
