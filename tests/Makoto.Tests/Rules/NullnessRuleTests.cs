namespace Makoto.Tests.Rules;

// Inside the namespace on purpose, in every file here: Makoto.Tests declares its own IsEmail, and an enclosing
// namespace beats a using at the top of the file.
using Makoto.Rules;

public class NullnessRuleTests
{
    private struct Point;

    [Fact]
    public void Reference_types()
    {
        Assert.False(((string?)null).NotNull());
        Assert.False(((object?)null).NotNull());
        Assert.False(((int[]?)null).NotNull());
        Assert.True("".NotNull());
        Assert.True(new object().NotNull());
        Assert.True(Array.Empty<int>().NotNull());
    }

    [Fact]
    public void Nullable_value_types()
    {
        Assert.False(((int?)null).NotNull());
        Assert.False(default(DateTime?).NotNull());
        Assert.False(default(Point?).NotNull());
        Assert.True(((int?)0).NotNull());
        Assert.True(((DateTime?)default(DateTime)).NotNull());
        Assert.True(((Point?)default(Point)).NotNull());
    }

    [Fact]
    public void Value_types_are_never_null()
    {
        Assert.True(0.NotNull());
        Assert.True(default(Point).NotNull());
    }

    [Fact]
    public void Works_through_a_generic_caller()
    {
        Assert.False(Check<string?>(null));
        Assert.False(Check<int?>(null));
        Assert.True(Check<int?>(1));
        Assert.True(Check(1));

        static bool Check<T>(T value) => value.NotNull();
    }
}
