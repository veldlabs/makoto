namespace Makoto.Tests;

public class FailureCollectorTests
{
    [Theory]
    [InlineData("", "items[7]")]
    [InlineData("Name", "items[7].Name")]
    public void Element_path_joins_the_child_path(string child, string expected)
    {
        var failures = default(FailureCollector);
        failures.Add(new ValidationFailure(child, "Code", "Message"));

        failures.PrefixFrom(0, "items", 7);

        Assert.Equal(expected, failures.ToResult().Failures[0].PropertyName);
    }

    [Fact]
    public void Element_path_longer_than_the_stack_buffer()
    {
        var path = new string('x', 300);
        var failures = default(FailureCollector);
        failures.Add(new ValidationFailure("Name", "Code", "Message"));

        failures.PrefixFrom(0, path, 7);

        Assert.Equal($"{path}[7].Name", failures.ToResult().Failures[0].PropertyName);
    }

    [Fact]
    public void Element_path_only_touches_failures_after_start()
    {
        var failures = default(FailureCollector);
        failures.Add(new ValidationFailure("Before", "Code", "Message"));
        failures.Add(new ValidationFailure("After", "Code", "Message"));

        failures.PrefixFrom(1, "items", 0);

        var result = failures.ToResult();
        Assert.Equal("Before", result.Failures[0].PropertyName);
        Assert.Equal("items[0].After", result.Failures[1].PropertyName);
    }
}
