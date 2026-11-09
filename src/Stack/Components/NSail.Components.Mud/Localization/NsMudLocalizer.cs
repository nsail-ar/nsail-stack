// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Localization;
using MudBlazor;
using NSail.Localization;

namespace NSail.Components;

// Feeds MudBlazor's own texts from the same string files as everything else, under the "Mud."
// prefix. Mud texts take positional placeholders ("{0}-{1} of {2}"), not the named ones NSail
// strings use: MudBlazor runs them through string.Format, which throws on a name.
internal sealed class NsMudLocalizer(StringManager strings) : MudLocalizer
{
    public override LocalizedString this[string key]
    {
        get
        {
            return strings.TryTranslate($"Mud.{key}", out var text)
                ? new LocalizedString(key, text)
                : new LocalizedString(key, key, resourceNotFound: true);
        }
    }
}
