// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Localization;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Feeds the catalog from an in-memory dictionary, the way a module's strings.json
/// does. Language-agnostic on purpose: LanguageProvider defaults to the ambient UI culture,
/// so a source that answered only "en" would make the test pass or fail by machine.</summary>
public sealed class FixedStrings(IReadOnlyDictionary<string, string> entries) : IStringSource
{
    public IReadOnlyCollection<string> Languages { get; } = [];

    public Task<IReadOnlyDictionary<string, string>> GetStrings(string language)
    {
        return Task.FromResult(entries);
    }
}
