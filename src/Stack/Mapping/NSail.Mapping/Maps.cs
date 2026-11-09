// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>The few shapes generated code needs that are not about entities.</summary>
public static class Maps
{
    /// <summary>One level deeper into the graph. Where the thread's stack is running short the
    /// rest of the walk continues on a fresh one, so a deep graph costs heap instead of
    /// overflowing — an overflow cannot be caught and would take the process down.</summary>
    public static async ValueTask Descend()
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            await Task.Yield();
        }
    }

    /// <summary>The text read as a <typeparamref name="T"/> in the invariant culture; a text that
    /// is none is the caller's invalid input, not a crash.</summary>
    public static T ParseValue<T>(string text, string field)
        where T : IParsable<T>
    {
        if (T.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw Invalid(field, text, typeof(T).Name);
    }

    /// <summary>The text read as an enum member, by name or number, ignoring case.</summary>
    public static T ParseEnum<T>(string text, string field)
        where T : struct, Enum
    {
        if (Enum.TryParse<T>(text, true, out var value))
        {
            return value;
        }

        throw Invalid(field, text, typeof(T).Name);
    }

    static NSail.Problems.BusinessException Invalid(string field, string text, string type)
    {
        return NSail.Problems.InputProblem.InvalidModel(new NSail.Problems.Issue(
            code: "InvalidValue",
            message: $"'{text}' is not a valid {type}.",
            source: field.Length == 0 ? null : field,
            arguments: new Dictionary<string, string> { ["value"] = text, ["type"] = type }));
    }

    /// <summary>A new list of mapped items, in the source's order; null for no source. A list of
    /// plain values or objects has no identity to merge by, so it is rebuilt.</summary>
    public static async Task<List<TTarget>?> List<TSource, TTarget>(IEnumerable<TSource>? source, Func<TSource, Task<TTarget>> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (source is null)
        {
            return null;
        }

        var result = new List<TTarget>();

        foreach (var item in source)
        {
            result.Add(await map(item).ConfigureAwait(false));
        }

        return result;
    }

    /// <summary>The items added to a collection of the target's own type, for a member typed as
    /// a collection a list cannot stand in for; null for no items.</summary>
    public static TCollection? Fill<TCollection, TItem>(TCollection collection, IEnumerable<TItem>? items)
        where TCollection : class, ICollection<TItem>
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (items is null)
        {
            return null;
        }

        foreach (var item in items)
        {
            collection.Add(item);
        }

        return collection;
    }
}
