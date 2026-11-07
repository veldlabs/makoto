using System.Collections.Immutable;

namespace Makoto.Rules;

// These take IEnumerable<T> because ICollection<T> and IReadOnlyCollection<T> are unrelated: overloads on the two are
// ambiguous for List<T>, arrays and every other type that implements both, and either one alone misses properties
// declared as the other. Sharing the name NotEmpty with strings is safe. string is IEnumerable<char>, but the string
// overload is an identity conversion, so it wins.

/// <summary>Passes when the collection has at least one element.</summary>
/// <typeparam name="T">The element type.</typeparam>
[ValidationRule(Name = "NotEmpty", Message = "'{PropertyName}' must not be empty.")]
public sealed class NotEmptyCollectionRule<T> : ValidationRule<IEnumerable<T>>
{
    /// <inheritdoc />
    public override bool Evaluate(IEnumerable<T> value) => Counting.UpTo(value, 1) != 0;
}

/// <summary>Passes when the collection has at least <c>min</c> elements.</summary>
/// <typeparam name="T">The element type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' must have at least {min} items.")]
public sealed class MinCountRule<T> : ValidationRule<IEnumerable<T>, int>
{
    /// <inheritdoc />
    public override bool Evaluate(IEnumerable<T> value, int min) => Counting.UpTo(value, min) >= min;
}

/// <summary>Passes when the collection has at most <c>max</c> elements.</summary>
/// <typeparam name="T">The element type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' must have at most {max} items.")]
public sealed class MaxCountRule<T> : ValidationRule<IEnumerable<T>, int>
{
    /// <inheritdoc />
    public override bool Evaluate(IEnumerable<T> value, int max) => Counting.UpTo(value, max + 1L) <= max;
}

internal static class Counting
{
    /// <summary>
    /// The exact count of a collection, without allocating. Anything else is enumerated and stops at
    /// <paramref name="limit"/>, so a lazy sequence is only read as far as the rule needs.
    /// </summary>
    public static long UpTo<T>(IEnumerable<T> source, long limit)
    {
        switch (source)
        {
            // A default ImmutableArray<T> throws from Count and from its enumerator. It is what a deserializer leaves
            // in an omitted property, so it counts as empty instead of failing the request with an exception.
            case ImmutableArray<T> array:
                return array.IsDefault ? 0 : array.Length;
            case ICollection<T> collection:
                return collection.Count;
            case IReadOnlyCollection<T> collection:
                return collection.Count;
        }

        var count = 0L;
        using var enumerator = source.GetEnumerator();
        while (count < limit && enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }
}
