using SecureFlow.Modules.Collaboration.Domain;
using SecureFlow.Modules.Notifications.Domain;

namespace SecureFlow.UnitTests;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Notification_normalizes_server_owned_fields_and_marks_read_idempotently()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();
        var notification = Notification.Create(
            organizationId,
            userId,
            " mentioned ",
            "  Başlık  ",
            "  Mesaj  ",
            " work_item ",
            Guid.NewGuid(),
            sourceEventId,
            Now);

        Assert.Equal("MENTIONED", notification.Type);
        Assert.Equal("Başlık", notification.Title);
        Assert.Equal("Mesaj", notification.Message);
        Assert.Equal("WORK_ITEM", notification.ResourceType);
        Assert.False(notification.IsRead);
        Assert.True(notification.MarkRead(Now.AddMinutes(1)));
        Assert.False(notification.MarkRead(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), notification.ReadAt);
    }

    [Fact]
    public void Notification_rejects_empty_ids_and_oversized_user_visible_fields()
    {
        Assert.Throws<ArgumentException>(() => Notification.Create(
            Guid.Empty,
            Guid.NewGuid(),
            "MENTIONED",
            "Başlık",
            "Mesaj",
            "WORK_ITEM",
            null,
            Guid.NewGuid(),
            Now));
        Assert.Throws<ArgumentException>(() => Notification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "MENTIONED",
            new string('x', Notification.TitleMaxLength + 1),
            "Mesaj",
            "WORK_ITEM",
            null,
            Guid.NewGuid(),
            Now));
    }

    [Fact]
    public void Preference_and_mention_updates_are_validated_and_idempotent()
    {
        var userId = Guid.NewGuid();
        var preference = NotificationPreference.Create(userId, false, Now);

        Assert.False(preference.Update(false, Now.AddMinutes(1)));
        Assert.True(preference.Update(true, Now.AddMinutes(2)));
        Assert.True(preference.EmailEnabled);
        Assert.Equal(Now.AddMinutes(2), preference.UpdatedAt);

        var mention = Mention.Create(Guid.NewGuid(), Guid.NewGuid(), userId, Now);
        Assert.Equal(userId, mention.UserId);
        Assert.Throws<ArgumentException>(() => Mention.Create(Guid.NewGuid(), Guid.Empty, userId, Now));
    }
}
