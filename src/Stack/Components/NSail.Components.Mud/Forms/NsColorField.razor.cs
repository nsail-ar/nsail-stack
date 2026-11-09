// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using MudBlazor.Utilities;

namespace NSail.Components;

public partial class NsColorField
{
    // Bound so the picker holds the model's color from its first render. Typed text it
    // rejects is not reset here: the box keeps showing that text while the model keeps the
    // last good hex.
    MudColor? MudValue => MudColor.TryParse(Value, out var color) ? color : null;

    // The same Value binding that fixes the refusal path also fires on first render: the
    // picker canonicalizes whatever text it is given (case included) and hands the result back
    // through TextChanged before anyone has touched the box. A model holding an uppercase hex
    // then sees a lowercase one pushed straight to ValueChanged, going dirty on arrival. Text
    // that parses to the same color already held is the picker's own echo, not an edit.
    Task SetColorValue(string? text)
    {
        if (MudColor.TryParse(text, out var incoming) && MudValue is { } current && incoming.RgbaEquals(current))
        {
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(text) && !NsColorText.IsColor(text))
        {
            return Task.CompletedTask;
        }

        return SetValue(text);
    }
}
