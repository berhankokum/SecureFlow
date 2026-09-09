using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Notifications.Domain;

namespace SecureFlow.Modules.Notifications.Infrastructure.Configurations;

public sealed class NotificationEmailDeliveryConfiguration
    : IEntityTypeConfiguration<NotificationEmailDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationEmailDelivery> builder)
    {
        builder.ToTable("NotificationEmailDeliveries", table =>
        {
            table.HasCheckConstraint(
                "CK_NotificationEmailDeliveries_Identity",
                "\"Id\" = \"NotificationId\"");
            table.HasCheckConstraint(
                "CK_NotificationEmailDeliveries_AttemptCount",
                $"\"AttemptCount\" BETWEEN 0 AND {NotificationEmailDelivery.MaximumAttempts}");
            table.HasCheckConstraint(
                "CK_NotificationEmailDeliveries_Status",
                "\"Status\" IN ('Pending', 'Sent', 'Failed', 'Skipped')");
            table.HasCheckConstraint(
                "CK_NotificationEmailDeliveries_State",
                "(\"Status\" = 'Pending' AND \"NextAttemptAt\" IS NOT NULL AND \"SentAt\" IS NULL) OR "
                + "(\"Status\" = 'Sent' AND \"NextAttemptAt\" IS NULL AND \"SentAt\" IS NOT NULL) OR "
                + "(\"Status\" IN ('Failed', 'Skipped') AND \"NextAttemptAt\" IS NULL AND \"SentAt\" IS NULL)");
        });
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.NotificationId).IsRequired();
        builder.Property(delivery => delivery.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(delivery => delivery.LastErrorCode)
            .HasMaxLength(NotificationEmailDelivery.LastErrorCodeMaxLength);
        builder.HasIndex(delivery => delivery.NotificationId)
            .IsUnique()
            .HasDatabaseName("UX_NotificationEmailDeliveries_NotificationId");
        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAt })
            .HasDatabaseName("IX_NotificationEmailDeliveries_Status_NextAttemptAt");
    }
}
