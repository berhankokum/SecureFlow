using SecureFlow.SharedKernel;

namespace SecureFlow.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_should_not_expose_an_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_should_expose_the_given_error()
    {
        var error = new Error("Test.Error", "Something went wrong.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Value_should_throw_when_result_is_a_failure()
    {
        var result = Result.Failure<int>(new Error("Test.Error", "Something went wrong."));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
