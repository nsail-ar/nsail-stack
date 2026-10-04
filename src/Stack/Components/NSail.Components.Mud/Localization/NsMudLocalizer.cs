// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Localization;
using MudBlazor;
using NSail.Localization;

namespace NSail.Components;

/// <summary>Feeds MudBlazor's own texts (pagination, empty tables, required errors, date
/// pickers) from the same string files as everything else, under the "Mud." prefix — the key
/// MudBlazor asks for is appended as-is ("Mud.MudTablePager_FirstPage"). A key we do not
/// define is reported as not found, so MudBlazor falls back to its built-in resource.
/// <para>Mud texts take <b>positional</b> placeholders ("{0}-{1} of {2}"), not the named ones
/// NSail strings use: MudBlazor runs them through string.Format, which throws on a name.</para></summary>
public sealed class NsMudLocalizer(StringManager strings) : MudLocalizer
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
