using System.Diagnostics;

namespace Makoto.Tests.Rules;

using Makoto.Rules;

public class AllocationTests
{
    private static readonly string?[] Strings = ["", "abc", "a@b", "0f8fad5b-d9cb-469f-a165-70867728950e", "12345", "http", null];
    private static readonly List<int> List = [1, 2, 3];
    private static readonly int[] Array = [1, 2, 3];
    private static readonly HashSet<int> Set = [1, 2, 3];
    private static readonly IReadOnlyList<int> ReadOnly = [1, 2, 3];
    private static readonly DateTime Date = new(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string> Cases => [.. Checks.Keys];

    private static readonly Dictionary<string, Func<bool>> Checks = new()
    {
        ["NotNull"] = static () => "a".NotNull(),
        ["NotNull int?"] = static () => ((int?)1).NotNull(),
        ["NotNull null int?"] = static () => ((int?)null).NotNull(),
        ["Strings"] = static () =>
        {
            var result = true;
            foreach (var value in Strings)
            {
                result ^= value.NotEmpty() ^ value.NotBlank() ^ value.MinLength(1) ^ value.MaxLength(1) ^ value.LengthBetween(1, 2);
                result ^= value.StartsWithText("a") ^ value.EndsWithText("c") ^ value.ContainsText("b");
                result ^= value.IsEmail() ^ value.IsGuid() ^ value.IsDigits() ^ value.IsAscii();
            }

            return result;
        },
        ["IsUrl failures"] = static () => "ftp://a.b".IsUrl() | "http".IsUrl() | "".IsUrl() | ((string?)null).IsUrl(),
        ["int"] = static () => 1.GreaterThan(0) & 1.GreaterThanOrEqual(0) & 1.LessThan(0) & 1.LessThanOrEqual(0) & 1.Between(0, 1),
        ["int?"] = static () => ((int?)1).GreaterThan(0) & ((int?)1).LessThan(0) & ((int?)1).Between(0, 1),
        ["decimal"] = static () => 1m.GreaterThan(0) & 1m.Between(0, 1),
        ["DateTime"] = static () => Date.GreaterThan(Date) & Date.Between(Date, Date),
        ["string comparison"] = static () => "b".GreaterThan("a") & ((string?)null).LessThan("a"),
        ["Collections"] = static () =>
            List.NotEmpty() & Array.NotEmpty() & Set.NotEmpty() & ReadOnly.NotEmpty()
            & List.MinCount(1) & Array.MinCount(1) & Set.MaxCount(1) & ReadOnly.MaxCount(1),
        ["IsDefined"] = static () => DayOfWeek.Friday.IsDefined() & ((DayOfWeek)99).IsDefined() & ((DayOfWeek?)DayOfWeek.Friday).IsDefined(),
    };

    // Tier-0 code boxes where optimized code doesn't, so this waits for tier-1 before giving up.
    [Theory]
    [MemberData(nameof(Cases))]
    public void Allocates_nothing(string name)
    {
        var check = Checks[name];
        var deadline = Stopwatch.StartNew();
        long allocated;

        do
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                check();
            }

            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated != 0)
            {
                Thread.Sleep(10);
            }
        }
        while (allocated != 0 && deadline.Elapsed < TimeSpan.FromSeconds(5));

        Assert.Equal(0, allocated);
    }

    // The two known allocations, which double as proof that the measurement above can see one.
    [Fact]
    public void Only_a_valid_url_and_a_lazy_sequence_allocate()
    {
        var lazy = List.Where(static _ => true);
        "http://a.b".IsUrl();
        lazy.NotEmpty();

        var before = GC.GetAllocatedBytesForCurrentThread();
        "http://a.b".IsUrl();
        var afterUrl = GC.GetAllocatedBytesForCurrentThread();
        lazy.NotEmpty();
        var afterLazy = GC.GetAllocatedBytesForCurrentThread();

        Assert.True(afterUrl > before);
        Assert.True(afterLazy > afterUrl);
    }
}
