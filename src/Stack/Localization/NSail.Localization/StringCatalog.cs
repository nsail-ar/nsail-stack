// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;

namespace NSail.Localization;

/// <summary>Holds one merged dictionary per supported language for the whole process, built from
/// the registered IStringSources on first use. A language the install does not carry answers the
/// default's dictionary, so the cache is bounded by the supported set whatever the caller passes.
/// GetAsync awaits the sources; Get serves the cache and builds only from sources that complete
/// synchronously, so it never blocks on a pending task.</summary>
public sealed class StringCatalog
{
    readonly IEnumerable<IStringSource> _sources;
    readonly LanguageOptions _options;
    readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _languages = new();

    public StringCatalog(IEnumerable<IStringSource> sources, LanguageOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = sources;
        _options = options ?? new LanguageOptions();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAsync(string language)
    {
        var key = Resolve(language);

        if (_languages.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var entries = new Dictionary<string, string>();

        foreach (var source in _sources)
        {
            foreach (var entry in await source.GetStrings(key).ConfigureAwait(false))
            {
                entries[entry.Key] = entry.Value;
            }
        }

        return _languages.GetOrAdd(key, entries);
    }

    /// <summary>The cached dictionary, built now when every source answers synchronously. A source
    /// still pending throws: await GetAsync for that language first.</summary>
    public IReadOnlyDictionary<string, string> Get(string language)
    {
        var key = Resolve(language);

        if (_languages.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var entries = new Dictionary<string, string>();

        foreach (var source in _sources)
        {
            var pending = source.GetStrings(key);

            if (!pending.IsCompleted)
            {
                throw new InvalidOperationException(
                    $"The strings for '{key}' are not loaded and a source is asynchronous: await StringCatalog.GetAsync('{key}') before Get.");
            }

            foreach (var entry in pending.GetAwaiter().GetResult())
            {
                entries[entry.Key] = entry.Value;
            }
        }

        return _languages.GetOrAdd(key, entries);
    }

    string Resolve(string language)
    {
        var supported = Languages.Supported(_sources, _options);

        return supported.FirstOrDefault(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase))
            ?? _options.Default;
    }
}
