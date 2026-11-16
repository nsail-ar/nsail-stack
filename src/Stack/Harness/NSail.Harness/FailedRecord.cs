// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections;
using System.Globalization;

namespace NSail.Harness;

// A log record's state: the template's values, then values it does not name, then the template, last,
// where the logging providers read the record's body from. A field rides beside the template without
// entering it, so what groups the record (its template) does not move with what it carries.
sealed class FailedRecord(string template, IReadOnlyList<KeyValuePair<string, object?>> values) : IReadOnlyList<KeyValuePair<string, object?>>
{
    public int Count => values.Count + 1;

    public KeyValuePair<string, object?> this[int index] => index < values.Count ? values[index] : new("{OriginalFormat}", template);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // The console's line: the template with the values it names written in.
    public override string ToString()
    {
        var line = template;
        foreach (var (key, value) in values)
        {
            line = line.Replace($"{{{key}}}", Convert.ToString(value, CultureInfo.InvariantCulture) ?? "(null)", StringComparison.Ordinal);
        }
        return line;
    }
}
