// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.JSInterop;
using NSail.Localization;

namespace NSail.Components;

/// <summary>The frame's own Help group, last on the map, opening with Atajos de teclado: the
/// one door the Stack plants, because the "?" sheet is the frame's and no kit or app owns it.
/// An app hangs its own help under it by naming <see cref="Group"/> as a Parent — a guide, a
/// way to reach support — and moves or hides the group as it would any kit's.</summary>
public sealed class HelpMenu(DialogManager dialogs, IJSRuntime js, StringManager strings) : INavMenuContributor
{
    public const string Group = "Help";

    // After every system group (navigation.md, weight bands): help is what a person reaches
    // for when the rest of the map did not answer.
    const int Weight = 1000;

    public Task<IReadOnlyList<NavMenuItem>> GetItems()
    {
        IReadOnlyList<NavMenuItem> items =
        [
            new NavMenuItem
            {
                Name = Group,
                Icon = NsIcons.Help,
                Weight = Weight,
                Items =
                [
                    new NavMenuItem
                    {
                        Name = "Shortcuts",
                        Icon = NsIcons.Keyboard,
                        Command = ShowKeys,
                        Chord = NsKeys.Help
                    }
                ]
            }
        ];

        return Task.FromResult(items);
    }

    // What the sheet lists is read off the DOM BEFORE it opens, so it is the screen as it stands
    // and not the screen with the sheet on top of it: a dialog does not unmount the page behind
    // it, and the sheet's own chrome claims a chord of its own.
    async Task ShowKeys()
    {
        var claimed = await js.InvokeAsync<NsChordsOnScreen>("nsapp.chords");

        await dialogs.Open<NsKeySheet>(
            strings.Translate("Common.Shortcuts"),
            new Dictionary<string, object?> { [nameof(NsKeySheet.Claimed)] = claimed });
    }
}
