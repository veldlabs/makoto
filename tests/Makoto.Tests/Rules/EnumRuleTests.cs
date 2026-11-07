namespace Makoto.Tests.Rules;

using Makoto.Rules;

public class EnumRuleTests
{
    public enum Color
    {
        Red,
        Green = 5,
        Blue = -1,
    }

    private enum Small : byte
    {
        Low = 0,
        High = byte.MaxValue,
    }

    [Flags]
    private enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
        ReadWrite = Read | Write,
    }

    [Theory]
    [InlineData(Color.Red, true)]
    [InlineData(Color.Green, true)]
    [InlineData(Color.Blue, true)]
    [InlineData((Color)1, false)]
    [InlineData((Color)4, false)]
    [InlineData((Color)6, false)]
    [InlineData((Color)int.MaxValue, false)]
    [InlineData((Color)int.MinValue, false)]
    public void Declared_members_pass(Color value, bool expected)
    {
        Assert.Equal(expected, value.IsDefined());
        Assert.Equal(expected, ((Color?)value).IsDefined());
    }

    [Fact]
    public void Null_fails()
    {
        Assert.False(((Color?)null).IsDefined());
        Assert.False(default(DayOfWeek?).IsDefined());
    }

    [Fact]
    public void Default_only_passes_when_declared()
    {
        Assert.True(default(Color).IsDefined());
        Assert.True(default(Access).IsDefined());
        Assert.False(default(StringSplitOptions?).IsDefined());
        Assert.True(default(StringSplitOptions).IsDefined());
    }

    [Fact]
    public void Works_on_other_underlying_types()
    {
        Assert.True(Small.High.IsDefined());
        Assert.False(((Small)1).IsDefined());
        Assert.True(DayOfWeek.Sunday.IsDefined());
    }

    [Fact]
    public void Flags_only_pass_when_the_combination_is_declared()
    {
        Assert.True(Access.Read.IsDefined());
        Assert.True((Access.Read | Access.Write).IsDefined());
        Assert.False((Access.Read | Access.Execute).IsDefined());
    }
}
