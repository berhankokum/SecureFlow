using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.UnitTests;

public class WorkItemTests
{
    private static readonly DateOnly Today = new(2026, 9, 2);
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_values_and_preserves_server_owned_identity()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var workItem = WorkItem.Create(
            organizationId,
            projectId,
            7,
            WorkItemType.Bug,
            "  Fix tenant boundary  ",
            "  Prevent cross-tenant reads  ",
            WorkItemPriority.Critical,
            reporterId,
            Today.AddDays(1),
            Today,
            Now);

        Assert.Equal(organizationId, workItem.OrganizationId);
        Assert.Equal(projectId, workItem.ProjectId);
        Assert.Equal(reporterId, workItem.ReporterId);
        Assert.Equal(7, workItem.Number);
        Assert.Equal("Fix tenant boundary", workItem.Title);
        Assert.Equal("Prevent cross-tenant reads", workItem.Description);
    }

    [Fact]
    public void Create_rejects_invalid_values_and_past_due_date()
    {
        Assert.Throws<ArgumentException>(() => Create(title: " "));
        Assert.Throws<ArgumentException>(() => Create(title: new string('a', WorkItem.TitleMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => Create(description: new string('a', WorkItem.DescriptionMaxLength + 1)));
        Assert.Throws<DomainException>(() => Create(dueDate: Today.AddDays(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(number: 0));
    }

    [Fact]
    public void Update_allows_naturally_overdue_date_but_rejects_a_new_past_date()
    {
        var workItem = Create(dueDate: Today);

        workItem.Update(
            WorkItemType.Story,
            "Updated",
            null,
            WorkItemPriority.High,
            Today,
            Today.AddDays(1),
            Now.AddDays(1));

        Assert.Equal(Today, workItem.DueDate);
        Assert.Throws<DomainException>(() => workItem.Update(
            WorkItemType.Task,
            "Invalid",
            null,
            WorkItemPriority.Low,
            Today.AddDays(-1),
            Today.AddDays(1),
            Now.AddDays(1)));
    }

    [Fact]
    public void Archive_is_idempotent_and_makes_work_item_read_only()
    {
        var workItem = Create();
        var archivedAt = Now.AddHours(1);

        workItem.Archive(archivedAt);
        workItem.Archive(archivedAt.AddHours(1));

        Assert.Equal(archivedAt, workItem.ArchivedAt);
        Assert.Throws<DomainException>(() => workItem.Update(
            WorkItemType.Task,
            "No",
            null,
            WorkItemPriority.Low,
            null,
            Today,
            Now.AddHours(2)));
        Assert.Throws<DomainException>(() => workItem.Assign(Guid.NewGuid(), Now.AddHours(2)));
        Assert.Throws<DomainException>(() => workItem.Unassign(Now.AddHours(2)));
    }

    [Fact]
    public void Assignment_supports_reassign_and_unassign_without_mutating_on_idempotent_calls()
    {
        var workItem = Create();
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();

        Assert.True(workItem.Assign(firstUser, Now.AddMinutes(1)));
        Assert.Equal(firstUser, workItem.AssigneeId);
        Assert.Equal(Now.AddMinutes(1), workItem.UpdatedAt);
        Assert.False(workItem.Assign(firstUser, Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), workItem.UpdatedAt);
        Assert.True(workItem.Assign(secondUser, Now.AddMinutes(3)));
        Assert.Equal(secondUser, workItem.AssigneeId);
        Assert.True(workItem.Unassign(Now.AddMinutes(4)));
        Assert.Null(workItem.AssigneeId);
        Assert.Equal(Now.AddMinutes(4), workItem.UpdatedAt);
        Assert.False(workItem.Unassign(Now.AddMinutes(5)));
        Assert.Equal(Now.AddMinutes(4), workItem.UpdatedAt);
        Assert.Throws<ArgumentException>(() => workItem.Assign(Guid.Empty, Now));
    }

    private static WorkItem Create(
        long number = 1,
        string title = "SecureFlow",
        string? description = null,
        DateOnly? dueDate = null) =>
        WorkItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            number,
            WorkItemType.Task,
            title,
            description,
            WorkItemPriority.Medium,
            Guid.NewGuid(),
            dueDate,
            Today,
            Now);
}
