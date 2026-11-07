using Makoto.Rules;
using BenchmarkDotNet.Attributes;

/// <summary>Each built-in rule over a batch of inputs, so one operation is long enough to measure reliably.</summary>
[MemoryDiagnoser]
public class RuleBenchmarks
{
    private const int Batch = 64;

    private static readonly string[] Emails = Repeat("mike@example.com", "first.last+tag@sub.example.org", "nope", "a@b@c");
    private static readonly string[] Blanks = Repeat("hello", "   ", "      x", "");
    private static readonly string[] Digits = Repeat("1234567890", "12a", "0", "");
    private static readonly string[] Guids = Repeat("7f2d3c1e-6a44-4e9b-9f43-2f5a1c8d0b11", "nope");
    private static readonly string[] Urls = Repeat("https://example.com/a/b?c=d", "http://x.io", "ftp://nope", "nope");
    private static readonly List<string>[] Lists = Repeat(new List<string> { "a" }, new List<string>());
    private static readonly string[][] Arrays = Repeat(new[] { "a" }, Array.Empty<string>());
    private static readonly IReadOnlyList<string>[] ReadOnlyLists = Repeat<IReadOnlyList<string>>(new List<string> { "a", "b" }, new[] { "a" });
    private static readonly HashSet<int>[] Sets = Repeat(new HashSet<int> { 1, 2 }, []);
    private static readonly int[] Ints = Repeat(5, 500, -1, 42);
    private static readonly decimal[] Decimals = Repeat(5m, 500m, -1m, 42m);
    private static readonly int?[] NullableInts = Repeat<int?>(5, null, 500, 42);
    private static readonly DayOfWeek[] Days = Repeat(DayOfWeek.Monday, DayOfWeek.Sunday, (DayOfWeek)99, DayOfWeek.Friday);

    private static T[] Repeat<T>(params T[] values) => Enumerable.Range(0, Batch).Select(i => values[i % values.Length]).ToArray();

    [Benchmark(OperationsPerInvoke = Batch)]
    public int IsEmail()
    {
        var passed = 0;
        foreach (var value in Emails)
        {
            passed += value.IsEmail() ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int NotBlank()
    {
        var passed = 0;
        foreach (var value in Blanks)
        {
            passed += value.NotBlank() ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int IsDigits()
    {
        var passed = 0;
        foreach (var value in Digits)
        {
            passed += value.IsDigits() ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int IsGuid()
    {
        var passed = 0;
        foreach (var value in Guids)
        {
            passed += value.IsGuid() ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int IsUrl()
    {
        var passed = 0;
        foreach (var value in Urls)
        {
            passed += value.IsUrl() ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int NotEmpty_List()
    {
        var passed = 0;
        foreach (var value in Lists)
        {
            passed += value.NotEmpty() ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int NotEmpty_Array()
    {
        var passed = 0;
        foreach (var value in Arrays)
        {
            passed += value.NotEmpty() ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int MinCount_ReadOnlyList()
    {
        var passed = 0;
        foreach (var value in ReadOnlyLists)
        {
            passed += value.MinCount(2) ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int MaxCount_HashSet()
    {
        var passed = 0;
        foreach (var value in Sets)
        {
            passed += value.MaxCount(1) ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int Between_Int()
    {
        var passed = 0;
        foreach (var value in Ints)
        {
            passed += value.Between(1, 100) ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int Between_Decimal()
    {
        var passed = 0;
        foreach (var value in Decimals)
        {
            passed += value.Between(1m, 100m) ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int GreaterThan_NullableInt()
    {
        var passed = 0;
        foreach (var value in NullableInts)
        {
            passed += value.GreaterThan(1) ? 1 : 0;
        }

        return passed;
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public int IsDefined_Enum()
    {
        var passed = 0;
        foreach (var value in Days)
        {
            passed += value.IsDefined() ? 1 : 0;
        }

        return passed;
    }
}
