// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Localization;

/// <summary>Supplies localized strings for a language (two-letter code). Sources merge in
/// registration order and the last one wins: kits, then product, then (future) database.</summary>
public interface IStringSource
{
    /// <summary>The languages this source carries, base language excluded. Empty when the set
    /// is not known at startup: request localization is configured once, so a language that
    /// appears later cannot become supported.</summary>
    IReadOnlyCollection<string> Languages { get; }

    /// <summary>A source feeding the render path (StringManager, which reads synchronously) must
    /// return a task already completed; one that awaits I/O is served only through
    /// StringCatalog.GetAsync, which the caller awaits before the render reads.</summary>
    Task<IReadOnlyDictionary<string, string>> GetStrings(string language);
}
