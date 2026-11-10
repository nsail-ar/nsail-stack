// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation;

/// <summary>A list that is equal to another by its items, so a model holding one is value-equal
/// and a pipeline step whose output has not changed is reported as unchanged.</summary>
public sealed class EquatableList<T> : List<T>, IEquatable<EquatableList<T>>
{
    public EquatableList()
    {
    }

    public EquatableList(IEnumerable<T> items) : base(items)
    {
    }

    public bool Equals(EquatableList<T>? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Count != other.Count)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;

        for (var i = 0; i < Count; i++)
        {
            if (!comparer.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as EquatableList<T>);
    }

    public override int GetHashCode()
    {
        var hash = 17;

        foreach (var item in this)
        {
            hash = unchecked(hash * 31 + (item?.GetHashCode() ?? 0));
        }

        return hash;
    }
}
