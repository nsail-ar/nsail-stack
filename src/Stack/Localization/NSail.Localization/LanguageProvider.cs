// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Localization;

/// <summary>The language strings resolve to. Defaults to the ambient UI culture — a request's
/// on the server, the browser's on the client. Set Current to switch language without a
/// reload: the catalog serves the new dictionary and the next render reads it.</summary>
public class LanguageProvider
{
    public virtual string Current { get; set; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
}
