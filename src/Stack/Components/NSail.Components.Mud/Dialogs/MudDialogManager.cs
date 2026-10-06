// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using MudBlazor;
using NSail.Localization;

namespace NSail.Components;

public sealed class MudDialogManager(IDialogService dialogs, ISnackbar snackbar, StringManager strings) : DialogManager
{
    // The hook ns-mud.css hangs pointer-events:none on. SnackbarTypeClass is the only per-toast
    // class the vendor concatenates onto the same box it marks pointer-events:auto, which is why
    // the rule can reach Notify without reaching Offer.
    public const string NotifyClass = "ns-toast";

    static readonly DialogOptions _openOptions = new()
    {
        CloseOnEscapeKey = true,
        // A misclick outside the dialog must not discard a half-filled form — the title
        // bar's X (NsOpenDialog) and Escape are the only ways out.
        BackdropClick = false,
        FullWidth = true,
        MaxWidth = MaxWidth.Medium
    };

    public override async Task<bool> Confirm(string message, string? title = null)
    {
        var result = await dialogs.ShowMessageBoxAsync(
            title ?? strings.Translate("Common.Confirm"),
            message,
            yesText: strings.Translate("Common.Yes"),
            cancelText: strings.Translate("Common.No"));

        return result == true;
    }

    public override async Task Alert(string message, string? title = null)
    {
        await dialogs.ShowMessageBoxAsync(
            title ?? strings.Translate("Common.Alert"),
            message,
            yesText: strings.Translate("Common.Ok"));
    }

    public override void Notify(string message, NsSeverity severity = NsSeverity.Info)
    {
        snackbar.Add(message, NsSeverityMapper.ToMud(severity), config =>
        {
            config.VisibleStateDuration = 5000;

            // Nothing to press: a five-second toast nobody dismisses, and a box that takes no
            // pointer events has no reachable icon to offer anyway. Dropping it is what lets
            // the click meant for the control underneath go through — see NotifyClass.
            config.ShowCloseIcon = false;
            config.SnackbarTypeClass = NotifyClass;
        });
    }

    public override void Offer(string message, string label, Action accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        snackbar.Add(message, Severity.Info, config =>
        {
            config.Action = label;
            config.ActionColor = Color.Primary;
            config.RequireInteraction = true;
            config.ShowCloseIcon = true;

            // The action button has no callback of its own — Options.Action only labels it,
            // and OnClick is the wire it actually rides, so the offer's word has to live here.
            // Setting Action also closes the vendor's body-tap fallback, which fires only when
            // there is no action button; the dedicated button is the one and only way in.
            config.OnClick = _ =>
            {
                accepted();

                return Task.CompletedTask;
            };
        });
    }

    public override async Task Open<TComponent>(
        string? title = null,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var reference = await ShowOpenDialog(typeof(TComponent), title, parameters);
        await reference.Result;
    }

    Task<IDialogReference> ShowOpenDialog(
        Type componentType,
        string? title,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var dialogParameters = new DialogParameters<NsOpenDialog>
        {
            { x => x.ComponentType, componentType },
            { x => x.Title, title },
            { x => x.ComponentParameters, parameters }
        };

        return dialogs.ShowAsync<NsOpenDialog>(string.Empty, dialogParameters, _openOptions);
    }
}
