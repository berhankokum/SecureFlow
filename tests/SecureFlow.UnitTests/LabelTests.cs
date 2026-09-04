using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.Modules.WorkItems.Presentation.Contracts;
using SecureFlow.SharedKernel;

namespace SecureFlow.UnitTests;

public class LabelTests
{
    [Fact]
    public void Label_canonicalizes_names_and_preserves_scope_and_creator_on_rename()
    {
        var now = DateTimeOffset.UtcNow;
        var label = Label.Create(Guid.NewGuid(), Guid.NewGuid(), "  Ｓｅｃｕｒｉｔｙ  ", Guid.NewGuid(), now);
        Assert.Equal("Security", label.Name);
        Assert.Equal("SECURITY", label.NormalizedName);
        var original = (label.Id, label.OrganizationId, label.ProjectId, label.CreatedBy, label.CreatedAt);
        label.Update(" urgent ", now.AddMinutes(1));
        Assert.Equal(original, (label.Id, label.OrganizationId, label.ProjectId, label.CreatedBy, label.CreatedAt));
        Assert.Equal("URGENT", label.NormalizedName);
        Assert.Equal(now.AddMinutes(1), label.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u3000")]
    public void Domain_and_request_validation_reject_empty_names(string? name)
    {
        Assert.False(Label.IsValidName(name));
        Assert.False(new LabelNameRequestValidator().Validate(new LabelNameRequest(name!)).IsValid);
    }

    [Fact]
    public void Name_length_is_checked_after_unicode_normalization()
    {
        Assert.True(Label.IsValidName(new string('a', 50)));
        Assert.False(Label.IsValidName(new string('a', 51)));
        Assert.False(Label.IsValidName(new string('\uFB03', 17))); // ffi expands to 51 characters.
    }

    [Fact]
    public void Assignment_rejects_foreign_project_tenant_and_archived_work_item()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var item = WorkItem.Create(Guid.NewGuid(), Guid.NewGuid(), 1, WorkItemType.Task, "Test", null,
            WorkItemPriority.Low, Guid.NewGuid(), userId, null, DateOnly.FromDateTime(now.UtcDateTime), now);
        var label = Label.Create(item.OrganizationId, item.ProjectId, "security", userId, now);
        var assignment = WorkItemLabel.Create(item, label, userId, now);
        Assert.Equal((item.Id, label.Id, userId, now),
            (assignment.WorkItemId, assignment.LabelId, assignment.AssignedBy, assignment.AssignedAt));
        Assert.Throws<DomainException>(() => WorkItemLabel.Create(item,
            Label.Create(item.OrganizationId, Guid.NewGuid(), "other", userId, now), userId, now));
        Assert.Throws<DomainException>(() => WorkItemLabel.Create(item,
            Label.Create(Guid.NewGuid(), item.ProjectId, "other", userId, now), userId, now));
        item.Archive(now);
        Assert.Throws<DomainException>(() => WorkItemLabel.Create(item, label, userId, now));
    }
}
