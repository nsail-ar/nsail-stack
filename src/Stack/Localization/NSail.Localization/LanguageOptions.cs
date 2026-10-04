// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Localization;

public class LanguageOptions
{
    /// <summary>The language the base "strings.json" is written in. It is the one language that
    /// cannot be derived: the base file carries no code in its name, and every source's
    /// Languages excludes it by contract — so an install that does not name it here can never
    /// render the catalog it is built on.</summary>
    public string Base { get; set; } = "en";

    /// <summary>The language a client gets when it has chosen none and its browser asks for one
    /// the install does not carry. A preference, not the base: catalogs written in English can
    /// serve an install that defaults to Spanish.</summary>
    public string Default { get; set; } = "en";

    /// <summary>The languages this install serves somebody who has chosen none — the set a
    /// browser's Accept-Language gets to vote among. Declared, because it cannot be derived:
    /// every language the catalogs can render carries the base one by construction, and an
    /// install that can render English is not an install that offers it. Empty is the common
    /// case and means the Default alone.</summary>
    public string[] Offered { get; set; } = [];
}
