using System.Text.RegularExpressions;

namespace Makoto.Tests;

[ValidationRule(Message = "'{PropertyName}' is not an email address.")]
public partial class IsEmailValidationRule : ValidationRule<string>
{
    public override bool Evaluate(string value) => EmailRegex.IsMatch(value);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex { get; }
}

[ValidationRule]
public sealed class IsPositiveRule : ValidationRule<int>
{
    public override bool Evaluate(int value) => value > 0;
}

[ValidationRule]
public sealed class AtMostRule<T> : ValidationRule<T, T>
    where T : IComparable<T>
{
    public override bool Evaluate(T value, T max) => value.CompareTo(max) <= 0;
}

public class CustomRuleTests
{
    [Fact]
    public void Reference_rule_becomes_a_null_safe_extension()
    {
        Assert.True("a@b.co".IsEmail());
        Assert.False("nope".IsEmail());
        Assert.False(((string?)null).IsEmail());
    }

    [Fact]
    public void Value_rule_gets_a_nullable_overload()
    {
        Assert.True(1.IsPositive());
        Assert.False(0.IsPositive());
        Assert.False(((int?)null).IsPositive());
    }

    [Fact]
    public void Generic_rule_with_an_argument()
    {
        Assert.True(3.AtMost(5));
        Assert.False("z".AtMost("a"));
    }
}
