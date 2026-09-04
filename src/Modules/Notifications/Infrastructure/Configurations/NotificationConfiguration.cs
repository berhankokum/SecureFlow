using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Notifications.Domain;

namespace SecureFlow.Modules.Notifications.Infrastructure.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Type)
            .HasMaxLength(Notification.TypeMaxLength)
            .IsRequired();
        builder.Property(notification => notification.Title)
            .HasMaxLength(Notification.TitleMaxLength)
            .IsRequired();
        builder.Property(notification => notification.Message)
            .HasMaxLength(Notification.MessageMaxLength)
            .IsRequired();
        builder.Property(notification => notification.ResourceType)
            .HasMaxLength(Notification.ResourceTypeMaxLength)
            .IsRequired();
        builder.HasIndex(notification => new { notification.UserId, notification.IsRead, notification.CreatedAt })
            .HasDatabaseName("IX_Notifications_UserId_IsRead_CreatedAt");
        builder.HasIndex(notification => new
        {
            notification.UserId,
            notification.Type,
            notification.SourceEventId
        })
            .IsUnique()
            .HasDatabaseName("UX_Notifications_UserId_Type_SourceEventId");
    }
}
