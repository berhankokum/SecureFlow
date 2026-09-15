using SecureFlow.Modules.WorkItems.Application;

namespace SecureFlow.UnitTests;

public class WorkItemPreconditionTests
{
    [Theory]
    [InlineData(null, "WorkItems.PreconditionRequired")]
    [InlineData("", "WorkItems.PreconditionRequired")]
    [InlineData("*", "WorkItems.InvalidPrecondition")]
    [InlineData("W/\"7\"", "WorkItems.InvalidPrecondition")]
    [InlineData("\"7\", \"8\"", "WorkItems.InvalidPrecondition")]
    [InlineData("7", "WorkItems.InvalidPrecondition")]
    [InlineData("\"07\"", "WorkItems.InvalidPrecondition")]
    [InlineData("\"8\"", "WorkItems.StaleVersion")]
    public void Rejects_invalid_or_stale_tokens(string? header, string code) =>
        Assert.Equal(code, new WorkItemPrecondition { Header = header }.Check(7)?.Code);

    [Fact]
    public void Accepts_current_strong_token() => Assert.Null(new WorkItemPrecondition { Header = "\"7\"" }.Check(7));
}
