// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;

namespace NSail.Localization;

/// <summary>Holds one merged dictionary per language for the whole process, built from the
/// registered IStringSources on first use. A server ends up holding every language it was
/// asked for; a client asks for one and never pays for the rest.</summary>
public sealed class StringCatalog
{
    readonly IEnumerable<IStringSource> _sources;
    readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _languages = new();

    public StringCatalog(IEnumerable<IStringSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = sources;
    }

    public IReadOnlyDictionary<string, string> Get(string language)
    {
        return _languages.GetOrAdd(language, Load);
    }

    IReadOnlyDictionary<string, string> Load(string language)
    {
        var entries = new Dictionary<string, string>();

        foreach (var source in _sources)
        {
            // Embedded sources complete synchronously; a future async source (database)
            // will be preloaded before first render instead of resolved here.
            foreach (var entry in source.GetStrings(language).GetAwaiter().GetResult())
            {
                entries[entry.Key] = entry.Value;
            }
        }

        return entries;
    }
}
