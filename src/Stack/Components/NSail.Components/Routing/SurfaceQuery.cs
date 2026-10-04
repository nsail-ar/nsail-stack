// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace NSail.Components;

/// <summary>
/// Both ends of a query string, in the one shape the framework writes: invariant formats, a key
/// repeated once per array element. Reading (GetValue) and writing (SetValue, Format) share the
/// same formatter here rather than agreeing by two people remembering, which is what makes a
/// write-then-read round trip identity. Which query string a page is actually routed by is the
/// surface's answer, not this one's — see SurfaceContext.
/// </summary>
public static class SurfaceQuery
{
    // Nulls stand for "this text is not one of these", so a failed parse falls back to the
    // caller's default instead of throwing — a hand-typed address is not an exception.
    static readonly Dictionary<Type, Func<string, object?>> s_parsers = new()
    {
        [typeof(Guid)] = text => Guid.TryParse(text, CultureInfo.InvariantCulture, out var value) ? value : null,
        [typeof(bool)] = text => bool.TryParse(text, out var value) ? value : null,
        [typeof(int)] = text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null,
        [typeof(long)] = text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null,
        [typeof(short)] = text => short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null,
        [typeof(decimal)] = text => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null,
        [typeof(double)] = text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null,
        [typeof(float)] = text => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null,
        [typeof(DateOnly)] = text => DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null,
        [typeof(TimeOnly)] = text => TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null,
        // Unspecified kind, deliberately — the same wall clock RouteTable's route constraints
        // read, which reading as local or UTC would move.
        [typeof(DateTime)] = text => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null,
        [typeof(DateTimeOffset)] = text => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null,
    };

    public static string? GetRoute(NavigationManager navigation, Surface surface)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentException.ThrowIfNullOrEmpty(surface.Name);

        var uri = navigation.ToAbsoluteUri(navigation.Uri);
        var query = Parse(uri.Query);

        return query.TryGetValue(surface.Name, out var route) && !string.IsNullOrWhiteSpace(route)
            ? route
            : null;
    }

    /// <summary>The path half of a route — the text before its query. A named surface's route
    /// travels as a query VALUE and is therefore a relative string with no AbsolutePath to ask
    /// for, which is why the split is here rather than on Uri. Null in, null out: a surface
    /// routed at nothing is not standing at any path.</summary>
    public static string? RoutePath(string? route)
    {
        var mark = route?.IndexOf('?') ?? -1;

        return mark < 0 ? route : route![..mark];
    }

    /// <summary>The query half of the same route, carrying its leading '?' — empty when the
    /// route has none, which is what the reading and writing sides both take as "no pairs".</summary>
    public static string RouteQuery(string? route)
    {
        var mark = route?.IndexOf('?') ?? -1;

        return mark < 0 ? string.Empty : route![mark..];
    }

    public static Dictionary<string, string> Parse(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in Pairs(query))
        {
            values[key] = value;
        }

        return values;
    }

    /// <summary>Every value carried under <paramref name="name"/>, in address order — an array
    /// travels as a repeated key.</summary>
    public static IReadOnlyList<string> GetValues(string query, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var values = new List<string>();

        foreach (var (key, value) in Pairs(query))
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                values.Add(value);
            }
        }

        return values;
    }

    /// <summary>Whether <paramref name="name"/> is carried at all; a repeated key answers with
    /// its last value, the one Parse keeps.</summary>
    public static bool TryGetValue(string query, string name, out string? value)
    {
        var values = GetValues(query, name);

        value = values.Count == 0 ? null : values[^1];

        return values.Count > 0;
    }

    /// <summary>The value under <paramref name="name"/> as <typeparamref name="T"/>, the
    /// default when it is absent or unreadable. Conversion is culture-invariant, like the
    /// route constraints and the URL builder on the other side of the trip; an array type
    /// collects every value the key carries.</summary>
    public static T? GetValue<T>(string query, string name)
    {
        var type = typeof(T);

        if (type.IsArray)
        {
            return (T?)GetArray(query, name, type);
        }

        if (!TryGetValue(query, name, out var text) || text is null)
        {
            return default;
        }

        return TryConvert(text, Nullable.GetUnderlyingType(type) ?? type, out var value) && value is not null
            ? (T)value
            : default;
    }

    /// <summary>The query string with <paramref name="name"/> carrying <paramref name="value"/>;
    /// a null value, or an empty sequence, removes the key. A scalar writes one pair; anything
    /// else enumerable (Guid[] and friends — never a string, which is enumerable in its own
    /// right) writes one pair per element, the same repeated-key shape GetValue's array branch
    /// reads back. Every other pair is copied verbatim and a key already present keeps the
    /// position each of its occurrences already held — extra new values append after the last
    /// one, extra old occurrences drop — so writing the same value twice gives back the same
    /// string, the property a surface's SetQuery rests on, since it composes one level of the
    /// address out of another. Carries its leading '?' and is empty when nothing is left.</summary>
    public static string SetValue(string query, string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var pairs = Pairs(name, value);
        var segments = new List<string>();
        var next = 0;

        foreach (var segment in Segments(query))
        {
            if (!IsKey(segment, name))
            {
                segments.Add(segment);
                continue;
            }

            if (next < pairs.Count)
            {
                segments.Add(pairs[next]);
                next++;
            }
        }

        while (next < pairs.Count)
        {
            segments.Add(pairs[next]);
            next++;
        }

        return segments.Count == 0 ? string.Empty : "?" + string.Join('&', segments);
    }

    static List<string> Pairs(string name, object? value)
    {
        var texts = new List<string>();

        if (value is string || value is not System.Collections.IEnumerable items)
        {
            if (value is not null)
            {
                texts.Add(Format(value));
            }
        }
        else
        {
            foreach (var item in items)
            {
                if (item is not null)
                {
                    texts.Add(Format(item));
                }
            }
        }

        return texts.ConvertAll(text => $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(text)}");
    }

    // The reading side is invariant — Blazor's own route constraints and query-parameter
    // binding, and TryConvert below — so the writing side has to be, or a request served in a
    // culture that writes 30/07/2026 builds a URL the router then refuses to match, with
    // nothing to read anywhere. The date and time types take a sortable format rather than
    // the invariant default: that one carries slashes, and an escaped slash in a path segment
    // is a fight with the host's own normalization.

    /// <summary>A value in the invariant shape both ends of a URL agree on — the writer behind
    /// RouteTable's URLs and behind SetValue alike.</summary>
    public static string Format(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime moment => moment.ToString("s", CultureInfo.InvariantCulture),
            DateTimeOffset moment => moment.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    static bool IsKey(string segment, string name)
    {
        var mark = segment.IndexOf('=');
        var key = Uri.UnescapeDataString(mark < 0 ? segment : segment[..mark]);

        return string.Equals(key, name, StringComparison.OrdinalIgnoreCase);
    }

    static object? GetArray(string query, string name, Type type)
    {
        var texts = GetValues(query, name);

        if (texts.Count == 0)
        {
            return null;
        }

        var elementType = type.GetElementType()!;
        var target = Nullable.GetUnderlyingType(elementType) ?? elementType;
        var items = new List<object?>(texts.Count);

        foreach (var text in texts)
        {
            if (TryConvert(text, target, out var value))
            {
                items.Add(value);
            }
        }

        var array = Array.CreateInstance(elementType, items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            array.SetValue(items[i], i);
        }

        return array;
    }

    static bool TryConvert(string text, Type type, out object? value)
    {
        if (type == typeof(string))
        {
            value = text;
            return true;
        }

        // An empty value ("?PartyId=") is the key being carried without one, so it reads as
        // absent rather than as a parse failure worth reporting.
        if (text.Length == 0)
        {
            value = null;
            return false;
        }

        if (type.IsEnum)
        {
            var parsed = Enum.TryParse(type, text, ignoreCase: true, out value);

            return parsed && value is not null;
        }

        if (s_parsers.TryGetValue(type, out var parser))
        {
            value = parser(text);

            return value is not null;
        }

        value = null;

        return false;
    }

    static IEnumerable<(string Key, string Value)> Pairs(string query)
    {
        foreach (var segment in Segments(query))
        {
            var pair = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]);

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            yield return (key, pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty);
        }
    }

    static IEnumerable<string> Segments(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            yield break;
        }

        var trimmed = query[0] == '?' ? query[1..] : query;

        foreach (var segment in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return segment;
        }
    }
}
