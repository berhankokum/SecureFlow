using SecureFlow.Modules.WorkItems.Application;

namespace SecureFlow.UnitTests;

public class WorkItemListQueryTests
{
    [Fact]
    public void Defaults_and_extreme_valid_dates_are_supported()
    {
        var validator = new WorkItemListQueryValidator();
        Assert.True(validator.Validate(new WorkItemListQuery()).IsValid);
        Assert.True(validator.Validate(new WorkItemListQuery
        {
            Page = 100000,
            PageSize = 100,
            CreatedTo = DateOnly.MaxValue,
            Number = long.MaxValue,
        }).IsValid);
        Assert.False(validator.Validate(new WorkItemListQuery { Number = 0 }).IsValid);
        Assert.False(validator.Validate(new WorkItemListQuery { Search = new string('x', 201) }).IsValid);
        Assert.False(validator.Validate(new WorkItemListQuery { Sort = "Id; DROP TABLE WorkItems" }).IsValid);
    }
}
