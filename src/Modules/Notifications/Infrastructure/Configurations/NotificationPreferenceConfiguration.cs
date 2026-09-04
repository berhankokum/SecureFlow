using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureFlow.Modules.Notifications.Domain;

namespace SecureFlow.Modules.Notifications.Infrastructure.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(preference => preference.UserId);
        builder.Ignore(preference => preference.Id);
    }
}
