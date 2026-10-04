// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using MudBlazor.Utilities;

namespace NSail.Components;

public partial class NsColorField
{
    // The vendor's own refusal path (typed text that fails MudColor.TryParse) resets its
    // displayed text from its OWN Value, not from what this field last handed TextChanged —
    // left unbound, that Value never gets a color at all and the box wipes to blank instead of
    // holding the last good hex. Feeding it back keeps that reset landing on the right color.
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

        return SetValue(text);
    }
}
