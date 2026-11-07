namespace Makoto.Tests.Rules;

using Makoto.Rules;

public class ComparisonRuleTests
{
    [Theory]
    [InlineData(6, 5, true)]
    [InlineData(5, 5, false)]
    [InlineData(4, 5, false)]
    [InlineData(int.MaxValue, int.MinValue, true)]
    [InlineData(int.MinValue, int.MaxValue, false)]
    public void GreaterThan(int value, int limit, bool expected)
    {
        Assert.Equal(expected, value.GreaterThan(limit));
        Assert.Equal(expected, ((int?)value).GreaterThan(limit));
    }

    [Theory]
    [InlineData(6, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(4, 5, false)]
    [InlineData(int.MaxValue, int.MaxValue, true)]
    [InlineData(int.MinValue, int.MaxValue, false)]
    public void GreaterThanOrEqual(int value, int limit, bool expected)
    {
        Assert.Equal(expected, value.GreaterThanOrEqual(limit));
        Assert.Equal(expected, ((int?)value).GreaterThanOrEqual(limit));
    }

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, false)]
    [InlineData(6, 5, false)]
    [InlineData(int.MinValue, int.MaxValue, true)]
    [InlineData(int.MaxValue, int.MinValue, false)]
    public void LessThan(int value, int limit, bool expected)
    {
        Assert.Equal(expected, value.LessThan(limit));
        Assert.Equal(expected, ((int?)value).LessThan(limit));
    }

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    [InlineData(int.MinValue, int.MinValue, true)]
    [InlineData(int.MaxValue, int.MinValue, false)]
    public void LessThanOrEqual(int value, int limit, bool expected)
    {
        Assert.Equal(expected, value.LessThanOrEqual(limit));
        Assert.Equal(expected, ((int?)value).LessThanOrEqual(limit));
    }

    [Theory]
    [InlineData(1, 1, 3, true)]
    [InlineData(2, 1, 3, true)]
    [InlineData(3, 1, 3, true)]
    [InlineData(0, 1, 3, false)]
    [InlineData(4, 1, 3, false)]
    [InlineData(2, 2, 2, true)]
    [InlineData(2, 3, 1, false)]
    [InlineData(int.MinValue, int.MinValue, int.MaxValue, true)]
    [InlineData(int.MaxValue, int.MinValue, int.MaxValue, true)]
    public void Between(int value, int min, int max, bool expected)
    {
        Assert.Equal(expected, value.Between(min, max));
        Assert.Equal(expected, ((int?)value).Between(min, max));
    }

    [Fact]
    public void Null_fails_every_comparison()
    {
        int? value = null;

        // Limits that a default(int) would pass, so these fail because of the null.
        Assert.False(value.GreaterThan(-1));
        Assert.False(value.GreaterThanOrEqual(0));
        Assert.False(value.LessThan(1));
        Assert.False(value.LessThanOrEqual(0));
        Assert.False(value.Between(int.MinValue, int.MaxValue));
    }

    [Fact]
    public void Decimal_with_constants_of_a_narrower_type()
    {
        Assert.True(1.5m.GreaterThan(1));
        Assert.False(1.00m.GreaterThan(1));
        Assert.True(1.00m.GreaterThanOrEqual(1));
        Assert.True(0.9999999999999999999999999999m.LessThan(1));
        Assert.True(decimal.MaxValue.Between(0, decimal.MaxValue));
        Assert.False(decimal.MinValue.Between(0, decimal.MaxValue));
    }

    [Fact]
    public void Nullable_decimal_with_constants_of_a_narrower_type()
    {
        decimal? value = 1.5m;
        decimal? missing = null;

        Assert.True(value.GreaterThan(1));
        Assert.True(value.GreaterThanOrEqual(1.5m));
        Assert.True(value.LessThan(2));
        Assert.True(value.LessThanOrEqual(1.5m));
        Assert.True(value.Between(1, 2));
        Assert.False(missing.GreaterThan(1));
        Assert.False(missing.Between(1, 2));
    }

    [Fact]
    public void DateTime_at_the_limits()
    {
        var limit = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(limit.AddTicks(1).GreaterThan(limit));
        Assert.False(limit.GreaterThan(limit));
        Assert.True(limit.GreaterThanOrEqual(limit));
        Assert.True(limit.AddTicks(-1).LessThan(limit));
        Assert.True(limit.LessThanOrEqual(limit));
        Assert.True(limit.Between(limit, limit));
        Assert.True(DateTime.MaxValue.GreaterThan(DateTime.MinValue));
    }

    [Fact]
    public void Nullable_DateTime()
    {
        var limit = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        DateTime? value = limit.AddTicks(1);
        DateTime? missing = null;

        Assert.True(value.GreaterThan(limit));
        Assert.True(value.GreaterThanOrEqual(limit));
        Assert.False(value.LessThan(limit));
        Assert.False(value.LessThanOrEqual(limit));
        Assert.True(value.Between(limit, DateTime.MaxValue));
        Assert.False(missing.GreaterThanOrEqual(DateTime.MinValue));
    }

    [Fact]
    public void Other_comparable_value_types()
    {
        Assert.True(2L.GreaterThan(1));
        Assert.True(((byte)2).GreaterThan((byte)1));
        Assert.True(1.5.Between(1, 2));
        Assert.True(1.5f.LessThan(2));
        Assert.True(TimeSpan.FromSeconds(2).GreaterThan(TimeSpan.FromSeconds(1)));
        Assert.True(new DateOnly(2026, 11, 5).LessThan(new DateOnly(2026, 11, 6)));
        Assert.True(DateTimeOffset.MaxValue.GreaterThan(DateTimeOffset.MinValue));
        Assert.True(((long?)2).GreaterThan(1));
        Assert.True(((double?)1.5).Between(1, 2));
    }

    [Fact]
    public void Reference_types_fail_on_null_instead_of_throwing()
    {
        Assert.True("b".GreaterThan("a"));
        Assert.True("a".Between("a", "b"));
        Assert.True(new Version(1, 2).LessThan(new Version(1, 3)));

        string? missing = null;
        Assert.False(missing.GreaterThan("a"));
        Assert.False(missing.GreaterThanOrEqual("a"));
        Assert.False(missing.LessThan("a"));
        Assert.False(missing.LessThanOrEqual("a"));
        Assert.False(missing.Between("a", "b"));
    }

    // Pinned, not endorsed: IComparable<T> orders NaN below every number, so it passes LessThan.
    [Fact]
    public void NaN_follows_CompareTo()
    {
        Assert.True(double.NaN.LessThan(0));
        Assert.True(double.NaN.LessThanOrEqual(0));
        Assert.False(double.NaN.GreaterThan(0));
        Assert.False(double.NaN.GreaterThanOrEqual(0));
        Assert.False(double.NaN.Between(double.NegativeInfinity, double.PositiveInfinity));
        Assert.True(((double?)double.NaN).LessThan(0));
    }
}
