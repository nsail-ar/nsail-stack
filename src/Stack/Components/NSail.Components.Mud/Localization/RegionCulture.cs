// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Localization;

namespace NSail.Components;

// A vendor formatter (currency symbol, date pattern) needs a real region: the app's own
// language is a neutral two-letter code (LanguageProvider, translation lookup only), and a
// neutral culture's format info carries no region-specific pattern. This is the one place
// the app language maps to a region, not one per field.
public static class RegionCulture
{
    static readonly Dictionary<string, string> _regions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "en-US",
        ["es"] = "es-AR",
    };

    // GetCultureInfo (not "new CultureInfo") returns the runtime's cached, read-only instance:
    // the SAME reference on every call for a given name. MudBlazor's own Culture parameter
    // change-detection compares by reference, so a fresh instance every render reads as a
    // constant change and drives the vendor into a render loop that fights the user's typing.
    public static CultureInfo For(LanguageProvider language)
    {
        return _regions.TryGetValue(language.Current, out var name) ? CultureInfo.GetCultureInfo(name) : CultureInfo.CurrentCulture;
    }
}
