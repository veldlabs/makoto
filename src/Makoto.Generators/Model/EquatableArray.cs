using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Makoto.Generators.Model;

/// <summary>An immutable array with value equality, so incremental pipeline models compare by content.</summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items = items;

    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public int Length => _items.IsDefault ? 0 : _items.Length;

    public T this[int index] => _items[index];

    // A loop, not SequenceEqual: this runs on every incremental comparison, and LINQ would box both structs and
    // allocate two enumerators each time.
    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        for (var i = 0; i < Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(_items[i], other._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in this)
        {
            hash = (hash * 31) + (item?.GetHashCode() ?? 0);
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)(_items.IsDefault ? ImmutableArray<T>.Empty : _items)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class EquatableArray
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> items)
        where T : IEquatable<T> => new(items.ToImmutableArray());
}
