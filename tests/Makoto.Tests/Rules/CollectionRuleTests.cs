using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Makoto.Tests.Rules;

using Makoto.Rules;

public class CollectionRuleTests
{
    /// <summary>Implements only IReadOnlyCollection, and enumerating it is a test failure.</summary>
    private sealed class ReadOnlyOnly(int count) : IReadOnlyCollection<int>
    {
        public int Count => count;

        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("Enumerated a collection.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void NotEmpty_on_every_collection_shape()
    {
        Assert.True(new List<int> { 1 }.NotEmpty());
        Assert.True(new[] { 1 }.NotEmpty());
        Assert.True(new HashSet<int> { 1 }.NotEmpty());
        Assert.True(new Dictionary<int, int> { [1] = 1 }.NotEmpty());
        Assert.True(new Queue<int>([1]).NotEmpty());
        Assert.True(new Stack<int>([1]).NotEmpty());
        Assert.True(new LinkedList<int>([1]).NotEmpty());
        Assert.True(new Collection<int> { 1 }.NotEmpty());
        Assert.True(new ReadOnlyCollection<int>([1]).NotEmpty());
        Assert.True(new ConcurrentBag<int> { 1 }.NotEmpty());
        Assert.True(ImmutableArray.Create(1).NotEmpty());
        Assert.True(ImmutableList.Create(1).NotEmpty());
        Assert.True(new ReadOnlyOnly(1).NotEmpty());

        Assert.False(new List<int>().NotEmpty());
        Assert.False(Array.Empty<int>().NotEmpty());
        Assert.False(new HashSet<int>().NotEmpty());
        Assert.False(new Dictionary<int, int>().NotEmpty());
        Assert.False(ImmutableArray<int>.Empty.NotEmpty());
        Assert.False(new ReadOnlyOnly(0).NotEmpty());
    }

    [Fact]
    public void NotEmpty_on_every_declared_interface()
    {
        var items = new List<int> { 1 };
        var none = new List<int>();

        Assert.True(((ICollection<int>)items).NotEmpty());
        Assert.True(((IList<int>)items).NotEmpty());
        Assert.True(((IReadOnlyCollection<int>)items).NotEmpty());
        Assert.True(((IReadOnlyList<int>)items).NotEmpty());
        Assert.True(((IEnumerable<int>)items).NotEmpty());

        Assert.False(((ICollection<int>)none).NotEmpty());
        Assert.False(((IList<int>)none).NotEmpty());
        Assert.False(((IReadOnlyCollection<int>)none).NotEmpty());
        Assert.False(((IReadOnlyList<int>)none).NotEmpty());
        Assert.False(((IEnumerable<int>)none).NotEmpty());
    }

    [Fact]
    public void Null_fails_every_collection_rule()
    {
        List<int>? missing = null;

        Assert.False(missing.NotEmpty());
        Assert.False(missing.MinCount(0));
        Assert.False(missing.MaxCount(10));
        Assert.False(((int[]?)null).NotEmpty());
        Assert.False(((IEnumerable<int>?)null).NotEmpty());
    }

    [Fact]
    public void NotEmpty_keeps_string_semantics_on_strings()
    {
        Assert.True("a".NotEmpty());
        Assert.False("".NotEmpty());
        Assert.False(((string?)null).NotEmpty());
    }

    [Fact]
    public void NotEmpty_on_a_collection_of_strings_counts_the_collection()
    {
        Assert.True(new List<string> { "" }.NotEmpty());
        Assert.False(new List<string>().NotEmpty());
    }

    [Theory]
    [InlineData(3, 2, true)]
    [InlineData(3, 3, true)]
    [InlineData(3, 4, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(0, -1, true)]
    [InlineData(3, int.MaxValue, false)]
    public void MinCount(int count, int min, bool expected)
    {
        foreach (var items in Shapes(count))
        {
            Assert.Equal(expected, items.MinCount(min));
        }
    }

    [Theory]
    [InlineData(3, 4, true)]
    [InlineData(3, 3, true)]
    [InlineData(3, 2, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, -1, false)]
    [InlineData(3, int.MaxValue, true)]
    public void MaxCount(int count, int max, bool expected)
    {
        foreach (var items in Shapes(count))
        {
            Assert.Equal(expected, items.MaxCount(max));
        }
    }

    [Fact]
    public void Lazy_sequences_are_only_read_as_far_as_needed()
    {
        var reads = 0;
        IEnumerable<int> Endless()
        {
            while (true)
            {
                reads++;
                yield return reads;
            }
        }

        Assert.True(Endless().NotEmpty());
        Assert.Equal(1, reads);

        reads = 0;
        Assert.True(Endless().MinCount(3));
        Assert.Equal(3, reads);

        reads = 0;
        Assert.False(Endless().MaxCount(3));
        Assert.Equal(4, reads);

        reads = 0;
        Assert.True(Endless().MinCount(0));
        Assert.Equal(0, reads);
    }

    [Fact]
    public void Covariant_view_of_a_collection_is_not_enumerated()
    {
        // IEnumerable<object> over a List<string> is not an ICollection<object>, but it is an IReadOnlyCollection<object>.
        IEnumerable<object> items = new List<string> { "a" };

        Assert.True(items.NotEmpty());
        Assert.True(items.MinCount(1));
        Assert.True(items.MaxCount(1));
    }

    private static IEnumerable<IEnumerable<int>> Shapes(int count)
    {
        var items = Enumerable.Range(0, count).ToList();

        yield return items;
        yield return items.ToArray();
        yield return items.ToHashSet();
        yield return new ReadOnlyOnly(count);
        yield return items.Where(_ => true);
    }
}
