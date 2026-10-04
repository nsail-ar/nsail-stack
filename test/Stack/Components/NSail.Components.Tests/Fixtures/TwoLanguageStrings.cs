// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Localization;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Mimics strings.json + strings.es.json merged per language — the shape
/// StringCatalog actually resolves in the real app — so a test that switches
/// LanguageProvider.Current sees exactly what a real es or en session would.</summary>
public sealed class TwoLanguageStrings(string key, string english, string spanish) : IStringSource
{
    public IReadOnlyCollection<string> Languages { get; } = ["es"];

    public Task<IReadOnlyDictionary<string, string>> GetStrings(string language)
    {
        IReadOnlyDictionary<string, string> entries = new Dictionary<string, string>
        {
            [key] = language == "es" ? spanish : english
        };

        return Task.FromResult(entries);
    }
}
