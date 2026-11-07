// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>A confirm dialog has nothing to type into, so what the cursor opens on is what a
/// bare Enter presses (ui/forms.md, <em>Focus on open</em>): an ordinary confirmation opens on
/// the act it offers, one that DESTROYS something opens on the way out. What is held here is the
/// word the dialog hands the focus trap and the order its two faces are drawn in — the trap does
/// the placing in a browser, so this is the mark the keystroke's safety is derived from, pinned
/// so the day the dialog stops saying it the failure has a name instead of being a document
/// thrown away by a key nobody meant to press.
///
/// <para>The other half — WHICH acts destroy, derived from the message's own verb and asked of
/// no screen — is held where that derivation lives (ConfirmSendInUseTests), and the keystroke
/// itself is walked in a browser: bUnit places no cursor.</para></summary>
public sealed class NsConfirmFocusTests : BunitContext, IAsyncLifetime
{
    public NsConfirmFocusTests()
    {
        Services.AddComponentServices();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Confirm"] = "Confirmar",
            ["Common.ConfirmSend"] = "¿Confirmás esta acción?",
            ["Common.Yes"] = "Sí",
            ["Common.No"] = "No",
            ["Common.Delete"] = "Eliminar",
            ["Common.Cancel"] = "Cancelar"
        })]));

        // Registered last so DI resolves it in place of MudBlazor's own KeyInterceptorService,
        // whose IAsyncDisposable-only shape bUnit's synchronous teardown cannot dispose
        // (HostDialogSurfaceTests' own note, which is also why teardown goes through
        // IAsyncLifetime below).
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    // The real dialog host, asked the way a screen asks it: Confirm completes when the dialog
    // CLOSES, so awaiting it here would hang the test.
    IRenderedComponent<DialogHostFixture> Ask(bool destroys)
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        _ = host.InvokeAsync(() => destroys
            ? dialogs.ConfirmDestruction("¿Eliminar este Color?")
            : dialogs.Confirm("¿Aprobar esta Orden de Compra?"));

        host.WaitForElement(".mud-dialog-actions button");

        return host;
    }

    static DefaultFocus? Opening(IRenderedComponent<DialogHostFixture> host)
    {
        return host.FindComponent<MudDialog>().Instance.DefaultFocus;
    }

    static IEnumerable<string> Faces(IRenderedComponent<DialogHostFixture> host)
    {
        return host.FindAll(".mud-dialog-actions button").Select(face => face.TextContent.Trim());
    }

    // The act's own face, which the order pinned below makes the second one.
    static MudButton Act(IRenderedComponent<DialogHostFixture> host)
    {
        return host.FindComponents<MudButton>().Last().Instance;
    }

    /// <summary>The two faces, in the order the house has always drawn them — the way out first,
    /// the act second — which is what makes "the first child" and "the last child" the two
    /// answers below mean something. An ordinary confirmation keeps the bare Sí/No; a destructive
    /// one wears the act itself on its face, "Eliminar", the generic word honest because Destroys
    /// is read off that same verb (NsPartial.Destroys).</summary>
    [Fact]
    public void AnOrdinaryConfirmation_DrawsTheWayOutBeforeTheAct()
    {
        Assert.Equal(["No", "Sí"], Faces(Ask(destroys: false)));
    }

    [Fact]
    public void ADestructiveConfirmation_DrawsTheWayOutBeforeTheAct()
    {
        Assert.Equal(["Cancelar", "Eliminar"], Faces(Ask(destroys: true)));
    }

    /// <summary>An ordinary confirmation offers its own act: the cursor stands on it, so Enter
    /// takes it — and the criterion "Enter activates the focused button" is answerable at all,
    /// which it is not when the trap parks on its own non-interactive fallback.</summary>
    [Fact]
    public void AnOrdinaryConfirmation_OpensOnTheActItOffers()
    {
        Assert.Equal(DefaultFocus.LastChild, Opening(Ask(destroys: false)));
    }

    /// <summary>And the one this exists for: a confirmation guarding a destructive act opens on
    /// the way out, so a reflex Enter can only decline.</summary>
    [Fact]
    public void ADestructiveConfirmation_OpensOnTheWayOut()
    {
        Assert.Equal(DefaultFocus.FirstChild, Opening(Ask(destroys: true)));
    }

    /// <summary>The act of a destructive question wears what it does: Danger's error fill
    /// (ui/actions.md), so the face says the cost before it is pressed and not only after. The
    /// cursor and the fill are the same derivation answering two questions.</summary>
    [Fact]
    public void ADestructiveConfirmation_WearsTheDestructiveRankOnItsAct()
    {
        var act = Act(Ask(destroys: true));

        Assert.Equal(Color.Error, act.Color);
        Assert.Equal(Variant.Filled, act.Variant);
    }

    /// <summary>And an ordinary one keeps the accent a confirmation has always answered with:
    /// nothing but a destructive act reaches the error fill, so the fill stays readable as
    /// destruction.</summary>
    [Fact]
    public void AnOrdinaryConfirmation_KeepsTheAccentOnItsAct()
    {
        var act = Act(Ask(destroys: false));

        Assert.Equal(Color.Primary, act.Color);
        Assert.Equal(Variant.Text, act.Variant);
    }

    /// <summary>The question itself is untouched by the door it came through: the title the
    /// opener passed and the message, drawn as they were when the vendor's message box drew
    /// them.</summary>
    [Fact]
    public void ADestructiveConfirmation_AsksTheSameQuestion()
    {
        var host = Ask(destroys: true);

        Assert.Contains("¿Eliminar este Color?", host.Find(".mud-dialog-content").TextContent);
        Assert.Contains("Confirmar", host.Find(".mud-dialog-title").TextContent);
    }

    /// <summary>A question has no grid to fit and no field to size, so the vendor's own
    /// width-to-content is wrong for it the same way it would be for a real form: a reasonable,
    /// fixed width (MudDialogManager's own _confirmOptions) reads as a dialog, not a tooltip
    /// shrink-wrapped to one sentence.</summary>
    [Fact]
    public void AConfirmation_OpensAtAFixedWidth()
    {
        var host = Ask(destroys: true);
        var dialogClass = host.Find(".mud-dialog").ClassList;

        Assert.Contains("mud-dialog-width-sm", dialogClass);
        Assert.Contains("mud-dialog-width-full", dialogClass);
    }

    /// <summary>Pressing a face answers the opener, which is the whole contract a screen holds
    /// this dialog to: the dialog closes and the answer is the face that was pressed.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task PressingAFace_AnswersTheOpenerAndClosesTheDialog(int face, bool expected)
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        var asked = host.InvokeAsync(() => dialogs.ConfirmDestruction("¿Eliminar este Color?"));

        host.WaitForElement(".mud-dialog-actions button");

        await host.InvokeAsync(() => host.FindAll(".mud-dialog-actions button")[face].Click());

        Assert.Equal(expected, await asked);
        host.WaitForAssertion(() => Assert.Empty(host.FindAll(".mud-dialog")));
    }

}
