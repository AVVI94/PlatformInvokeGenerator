using System;
using System.Collections;
using System.Collections.Generic;

namespace PlatformInvokeGenerator;

/// <summary>
/// Immutable array with value equality, used in the incremental generator model.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
{
    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    readonly T[]? _array;

    public EquatableArray(T[] array) => _array = array;

    public int Length => _array?.Length ?? 0;
    public bool IsEmpty => Length == 0;
    public T this[int index] => _array![index];

    public T[] ToArray() => _array is null ? Array.Empty<T>() : (T[])_array.Clone();

    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length)
            return false;
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < Length; i++)
        {
            if (!comparer.Equals(_array![i], other._array![i]))
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < Length; i++)
                hash = hash * 31 + (_array![i] is { } item ? comparer.GetHashCode(item) : 0);
            return hash;
        }
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
