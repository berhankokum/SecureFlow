using SecureFlow.Modules.Collaboration.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.UnitTests;

public class CommentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_content_and_rejects_invalid_input()
    {
        var comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Kontrol edildi.  ", Now);

        Assert.Equal("Kontrol edildi.", comment.Content);
        Assert.Equal(Now, comment.CreatedAt);
        Assert.Equal(Now, comment.UpdatedAt);
        Assert.Throws<ArgumentException>(() =>
            Comment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "   ", Now));
        Assert.Throws<ArgumentException>(() =>
            Comment.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new string('x', Comment.ContentMaxLength + 1),
                Now));
    }

    [Fact]
    public void Update_changes_only_distinct_active_content()
    {
        var comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "İlk", Now);

        Assert.False(comment.Update(" İlk ", Now.AddMinutes(1)));
        Assert.Equal(Now, comment.UpdatedAt);
        Assert.True(comment.Update("İkinci", Now.AddMinutes(2)));
        Assert.Equal("İkinci", comment.Content);
        Assert.Equal(Now.AddMinutes(2), comment.UpdatedAt);
    }

    [Fact]
    public void Delete_is_idempotent_and_blocks_future_updates()
    {
        var comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "İçerik", Now);

        Assert.True(comment.Delete(Now.AddMinutes(1)));
        Assert.False(comment.Delete(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), comment.DeletedAt);
        Assert.Throws<DomainException>(() => comment.Update("Değişiklik", Now.AddMinutes(3)));
    }

    [Fact]
    public void Activity_event_requires_valid_scope_actor_type_and_json_metadata()
    {
        var activity = ActivityEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            " work_item_created ",
            new Dictionary<string, string?> { ["workItemKey"] = "SF-1" },
            Now);

        Assert.Equal("WORK_ITEM_CREATED", activity.Type);
        Assert.Contains("SF-1", activity.Metadata, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => ActivityEvent.Create(
            Guid.Empty,
            null,
            null,
            Guid.NewGuid(),
            "TYPE",
            new Dictionary<string, string?>(),
            Now));
    }
}
