// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Regression coverage for a33dbe8: a component opened with DialogManager.Open (the
/// host-managed dialog surface — LocationForm, SetPasswordForm and the rest)
/// hands its TitleContent to MudDialog, which MudDialogProvider renders in its own tree,
/// above NsOpenDialog — outside the CascadingValue NsSurfaceContext establishes. Before the
/// fix, NsClose in the title row read a null SurfaceContext and erased itself (NsClose only
/// renders "Surface is not null &amp;&amp; !Surface.IsMain"), so the title's X never rendered
/// and Escape was the only way out. The fix re-provides the surface NsSurfaceContext hands its
/// content around the title row specifically (NsOpenDialog.razor, the CascadingValue inside
/// TitleContent) — this test exercises exactly that path, not the routed aside/modal
/// surfaces, which take a different route (NsCloseOnEscape) and are out of scope here.</summary>
public sealed class HostDialogSurfaceTests : BunitContext, IAsyncLifetime
{
    public HostDialogSurfaceTests()
    {
        Services.AddComponentServices();
        // Registered last so DI resolves it in place of MudBlazor's own KeyInterceptorService,
        // whose IAsyncDisposable-only shape bUnit's synchronous teardown cannot dispose.
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudDialogProvider (rendered by DialogHostFixture) resolves an internal MudBlazor
    // service that is IAsyncDisposable-only, same shape as IKeyInterceptorService above but
    // this one cannot be replaced from outside the MudBlazor assembly (the interface itself
    // is internal). Routing teardown through xunit's IAsyncLifetime instead of plain
    // IDisposable makes xunit call BunitContext's async DisposeAsync instead of its
    // synchronous Dispose, which is the one that fails trying to dispose it synchronously.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public void TitleContent_ReceivesTheDialogSurface_SoContentAndTitleAgree()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogProbeBody>("Dialog title"));

        // The content pane gets the surface directly from NsSurfaceContext's own cascade —
        // this was never broken. It is the baseline the title row's close icon must match.
        host.WaitForAssertion(() => Assert.Contains("dialog", host.Find(".surface-name").TextContent));
    }

    [Fact]
    public void TitleRow_RendersExactlyOneCloseButton()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogProbeBody>("Dialog title"));

        // Before a33dbe8, TitleContent's NsClose found no cascaded Surface and rendered
        // nothing at all — this read finds no element against the pre-fix shape. NsClose is
        // the only close affordance DialogProbeBody's content pane offers, so a single match
        // here is exactly the title row's icon.
        var closeButton = host.WaitForElement(".mud-dialog-title button");
        Assert.NotNull(closeButton);
    }

    [Fact]
    public async Task ClickingTheTitleClose_ClosesTheDialog()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogProbeBody>("Dialog title"));

        host.WaitForAssertion(() => Assert.True(host.FindAll(".dialog-probe-body").Count == 1));

        await host.InvokeAsync(() => host.Find(".mud-dialog-title button").Click());

        Assert.Empty(host.FindAll(".dialog-probe-body"));
    }

    /// <summary>A dialog-hosted form's header X is refused while that form refuses writes, exactly
    /// as the footer's Cancelar is. Both faces of the exit are read in one assertion because the
    /// defect was precisely that they disagreed: the Cancelar stands inside the form's cascade and
    /// greyed, while the X — drawn in TitleContent, which the vendor renders above the hosted
    /// component — read the default and stayed live (nsail#1484).</summary>
    [Fact]
    public void TheHeaderX_IsRefusedWithTheFooterCancelar_WhenTheHostedFormRefusesWrites()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogFormProbeBody>("Dialog title", Body(disabled: true)));

        host.WaitForAssertion(() =>
        {
            Assert.True(host.Find(".mud-dialog-title button").HasAttribute("disabled"));
            Assert.True(host.Find(".probe-cancel").HasAttribute("disabled"));
        });

        // And Escape stays, which is a constraint and not an omission: a form standing disabled
        // refuses writes for as long as its screen says so, a dialog takes no backdrop click and no
        // Back, and withdrawing the last exit for a refusal with no end traps the reader inside it.
        Assert.Equal("True", host.Find(".probe-escape").TextContent);
    }

    /// <summary>The same refusal for the length of a save: pressing the X mid-save closes a surface
    /// whose save is still running, which is the thing NsClose exists to refuse. The form's word
    /// travels up through the surface (SurfaceContext.FormRefuses) and back down into the title row
    /// through NsDialogExit, whose subscription to StateChanged is what redraws the X at all.</summary>
    [Fact]
    public void TheHeaderX_IsRefused_WhileTheHostedFormIsSaving()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogFormProbeBody>("Dialog title", Body(gate: gate)));

        // The baseline the refusal is read against: with nothing saving the X works, so the read
        // below is the save's answer and not a control that was never live.
        host.WaitForAssertion(() => Assert.False(host.Find(".mud-dialog-title button").HasAttribute("disabled")));

        // Unwaited: the submit's own dispatch returns only when the handler does, and this handler
        // is holding the save open on purpose — the poll below is the wait.
        _ = host.InvokeAsync(() => host.Find(".probe-submit").Click());

        host.WaitForAssertion(() =>
        {
            Assert.True(host.Find(".mud-dialog-title button").HasAttribute("disabled"));
            Assert.True(host.Find(".probe-cancel").HasAttribute("disabled"));
        });

        gate.SetResult();

        // A save that took closes the dialog on its own (NsForm.Finishes, a hosted surface), so the
        // refusal read above was this save's and the save ran through to its end under it.
        host.WaitForAssertion(() => Assert.Empty(host.FindAll(".dialog-form-body")));
    }

    /// <summary>A dialog hosting no form is unaffected — the surface carries no form's word, so the
    /// default is no refusal. The X of an informative dialog stays the way out it always was.</summary>
    [Fact]
    public void TheHeaderX_StaysLive_WhenTheDialogHostsNoForm()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogProbeBody>("Dialog title"));

        host.WaitForAssertion(() => Assert.False(host.Find(".mud-dialog-title button").HasAttribute("disabled")));
    }

    /// <summary>Escape is the exit's second face and the vendor's own handler: it closes the dialog
    /// instance directly, past Surface.Close() and past every refusal the X draws, and with
    /// BackdropClick off those two are the only ways out. So NsOpenDialog withdraws the key for the
    /// length of the save and hands it back as the opener asked for it (MudDialogManager's own
    /// CloseOnEscapeKey, never a second literal) — the refusal reaching it from NsDialogExit, since
    /// the dialog instance's cascade does not reach TitleContent. What the vendor does with the
    /// option is the vendor's business, the same trust BackdropClick already runs on; what is pinned
    /// here is the option this Stack sets, read off the dialog instance itself.</summary>
    [Fact]
    public void Escape_IsWithdrawnWhileTheHostedFormIsSaving_AndGivenBackAfterwards()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogFormProbeBody>("Dialog title", Body(gate: gate, aborts: true)));

        host.WaitForAssertion(() => Assert.Equal("True", host.Find(".probe-escape").TextContent));

        // Unwaited: the submit's own dispatch returns only when the handler does, and this handler
        // is holding the save open on purpose — the poll below is the wait.
        _ = host.InvokeAsync(() => host.Find(".probe-submit").Click());

        host.WaitForAssertion(() => Assert.Equal("False", host.Find(".probe-escape").TextContent));

        gate.SetResult();

        // The save ends refused, so the dialog is still standing and the way out is owed back.
        host.WaitForAssertion(() => Assert.Equal("True", host.Find(".probe-escape").TextContent));
        Assert.False(host.Find(".mud-dialog-title button").HasAttribute("disabled"));
    }

    static Dictionary<string, object?> Body(
        bool disabled = false,
        TaskCompletionSource? gate = null,
        bool aborts = false)
    {
        return new Dictionary<string, object?>
        {
            [nameof(DialogFormProbeBody.Model)] = new CheckBoxTrackingModel(),
            [nameof(DialogFormProbeBody.Disabled)] = disabled,
            [nameof(DialogFormProbeBody.Gate)] = gate,
            [nameof(DialogFormProbeBody.Aborts)] = aborts
        };
    }

    /// <summary>Leonardo's screenshot (Nueva Dirección): GUARDAR half-cut below the dialog
    /// edge, the footer travelling inside the same scroll as the fields instead of staying
    /// pinned. Root cause: MudBlazor's own .mud-dialog-content is a plain block box (flex:1 1
    /// auto + overflow:auto as a flex ITEM of .mud-dialog, never display:flex itself), so a
    /// hosted NsPanel's own flex-1/min-h-0 root classes were inert — nothing there is a flex
    /// item of anything, and .mud-dialog-content ends up the one and only scroll owner over
    /// the whole hosted form, footer included. bUnit lays nothing out, so what is pinned here
    /// is the DOM shape the CSS chain depends on: ContentClass restoring the flex column on
    /// .mud-dialog-content, and the footer surviving as NsPanel's own sibling of the content
    /// region rather than a descendant of it.</summary>
    [Fact]
    public void ADialogHostedNsPanel_GetsTheFlexColumnChainThatLetsItsFooterStayPinned()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogPanelProbeBody>("Dialog title"));

        // Without this, NsForm's display:contents EditForm hands a hosted NsPanel straight to
        // .mud-dialog-content as a plain block child — flex-1/min-h-0 do nothing there.
        var dialogContent = host.WaitForElement(".mud-dialog-content");
        Assert.Contains("d-flex", dialogContent.ClassList);
        Assert.Contains("flex-column", dialogContent.ClassList);

        // NsPanel's root becomes a genuine flex item of that box, so its own flex-1/min-h-0
        // classes (dead weight before the fix) finally have something to shrink against.
        var panel = dialogContent.QuerySelector(".ns-container");
        Assert.NotNull(panel);
        Assert.Contains("flex-1", panel!.ClassList);
        Assert.Contains("min-h-0", panel.ClassList);

        // The footer is NsPanel's own sibling of the content region, never nested under it —
        // the shape that keeps it out of the content's own overflow-y-auto scroll.
        var footer = dialogContent.QuerySelector(".ns-panel-footer");
        Assert.NotNull(footer);
        Assert.Null(footer!.QuerySelector(".probe-content"));
        Assert.NotNull(footer.QuerySelector(".probe-submit"));
    }
}
