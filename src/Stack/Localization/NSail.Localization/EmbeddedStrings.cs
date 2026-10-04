// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;
using System.Text.Json;

namespace NSail.Localization;

/// <summary>Reads "strings.json" (the base language) overlaid with "strings.{language}.json"
/// from an assembly's embedded resources — appsettings-style flat key→text files at the
/// project root. Each source self-completes, so a language with gaps falls back to the base.</summary>
sealed class EmbeddedStrings(Assembly assembly) : IStringSource
{
    const string Marker = ".strings.";
    const string Extension = ".json";

    public IReadOnlyCollection<string> Languages { get; } = Discover(assembly);

    public Task<IReadOnlyDictionary<string, string>> GetStrings(string language)
    {
        var entries = new Dictionary<string, string>();

        Merge(entries, ".strings.json");
        Merge(entries, $".strings.{language}.json");

        return Task.FromResult<IReadOnlyDictionary<string, string>>(entries);
    }

    void Merge(Dictionary<string, string> entries, string suffix)
    {
        var name = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            return;
        }

        using var stream = assembly.GetManifestResourceStream(name)!;

        foreach (var entry in JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [])
        {
            entries[entry.Key] = entry.Value;
        }
    }

    static IReadOnlyCollection<string> Discover(Assembly assembly)
    {
        return assembly
            .GetManifestResourceNames()
            .Select(GetLanguage)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static string? GetLanguage(string resource)
    {
        if (!resource.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // "X.strings.json" is the base language and carries no code, so the marker has to be
        // looked for with its trailing dot: only "X.strings.es.json" leaves something behind it.
        var body = resource[..^Extension.Length];
        var marker = body.LastIndexOf(Marker, StringComparison.OrdinalIgnoreCase);

        if (marker < 0)
        {
            return null;
        }

        var language = body[(marker + Marker.Length)..];

        return language.Length > 0 ? language : null;
    }
}
