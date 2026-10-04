// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>Leonardo, 2026-08-05: a form saved inside an aside leaves the surface open and the
/// user sits there "esperando a que pase algo" — "aplica a todos los casos". The rule pinned
/// here: a successful submit closes the surface that hosts the form when that surface is an
/// overlay (aside, modal, dialog), and changes nothing on the main surface, where master/detail
/// doctrine keeps the page open and repopulates. The case that was actually broken is the
/// common one — every create page navigates in place to the new record's edit route on save
/// (create-thin-then-enrich), and that navigation was reading as "the handler already said
/// where the user goes", so the aside stayed. It is main-surface doctrine, and inside an
/// overlay the close wins.</summary>
public sealed class NsFormCloseOnSubmitTests : BunitContext, IAsyncLifetime
{
    readonly CountingDialogManager _dialogs = new();
    readonly List<string> _locations = [];

    public NsFormCloseOnSubmitTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<DialogManager>(_ => _dialogs);
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(NsFormCloseOnSubmitTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    NavigationManager Navigation
    {
        get
        {
            var navigation = Services.GetRequiredService<NavigationManager>();

            if (_locations.Count == 0)
            {
                navigation.LocationChanged += OnLocationChanged;
            }

            return navigation;
        }
    }

    void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _locations.Add(args.Location);
    }

    /// <summary>Puts the address where a page that opened an aside leaves it, then renders the
    /// form inside that aside — the surface reads its own route from the query parameter, so
    /// "the aside is open" and "the aside closed" are facts about the address, exactly as they
    /// are in the app.</summary>
    IRenderedComponent<SurfaceFormHost> RenderInAside(Action<ComponentParameterCollectionBuilder<SurfaceFormHost>>? extra = null)
    {
        var navigation = Navigation;

        navigation.NavigateTo("/directory/parties?aside=directory%2Fparties%2Fnew");
        _locations.Clear();

        return Render<SurfaceFormHost>(p =>
        {
            p.Add(x => x.RouteTable, BuildRouteTable());
            p.Add(x => x.Model, new SubmitTrackingModel());
            p.Add(x => x.Name, Surfaces.Aside);
            extra?.Invoke(p);
        });
    }

    [Fact]
    public async Task ASuccessfulSubmitInAnAside_ClosesIt()
    {
        var host = RenderInAside();

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.DoesNotContain("aside=", Navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>The live reproduction: creating a Persona from a lookup. CreatePartyPage sits in
    /// the aside, saves, and navigates in place to UpdatePartyPage — so the aside used to stay
    /// open showing the edit form of the row that was just created, with nothing telling the
    /// user the save took. The close supersedes that navigation, and it supersedes it rather
    /// than racing it: the edit page must never be mounted only to be torn down.</summary>
    [Fact]
    public async Task ASubmitWhoseHandlerNavigatesInPlace_ClosesTheAsideInsteadOfMovingIt()
    {
        var host = RenderInAside(p => p.Add(x => x.NavigatesTo, "directory/parties/7/edit"));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.DoesNotContain("aside=", Navigation.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(_locations, location => location.Contains("parties%2F7%2Fedit", StringComparison.Ordinal));
        Assert.Single(_locations);
    }

    /// <summary>Success only. A refusal re-marks the form dirty and the surface stays: closing
    /// would destroy the message the form just drew, which is the one answer it is not allowed
    /// to lose (67477552).</summary>
    [Fact]
    public async Task ASubmitTheServerRefused_KeepsTheAsideOpenAndDrawsTheMessage()
    {
        var problem = new Problem("RuleViolation", "The operation could not be completed",
            [new Issue("RuleViolation", "El eje va de 1 a 180 grados.", "AxisRange")], 422);

        var host = RenderInAside(p => p.Add(x => x.Problem, problem));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Contains("aside=", Navigation.Uri, StringComparison.Ordinal);
        Assert.Contains("El eje va de 1 a 180 grados.", host.Find(".ns-form-problem").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedSubmitWhoseHandlerWouldHaveNavigated_KeepsTheAsideOpen()
    {
        var problem = new Problem("Unknown", "Something went wrong", [], 500);

        var host = RenderInAside(p => p
            .Add(x => x.Problem, problem)
            .Add(x => x.NavigatesTo, "directory/parties/7/edit"));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Contains("aside=", Navigation.Uri, StringComparison.Ordinal);
        Assert.Empty(_locations);
    }

    /// <summary>Master/detail, unchanged: an edit page on the main surface stays open after a
    /// save and repopulates, and a create page's in-place navigation to the new record's edit
    /// route is exactly that doctrine — neither is touched by this rule.</summary>
    [Fact]
    public async Task OnTheMainSurface_ASuccessfulSubmitKeepsThePageOpen()
    {
        var navigation = Navigation;

        navigation.NavigateTo("/directory/parties/new");
        _locations.Clear();

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel()));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.EndsWith("/directory/parties/new", navigation.Uri, StringComparison.Ordinal);
        Assert.Empty(_locations);
    }

    [Fact]
    public async Task OnTheMainSurface_ACreatePagesInPlaceNavigationStillHappens()
    {
        var navigation = Navigation;

        navigation.NavigateTo("/directory/parties/new");
        _locations.Clear();

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.NavigatesTo, "directory/parties/7/edit"));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.EndsWith("/directory/parties/7/edit", navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>A filter or a search carries no document to finish, and a routed overlay is a
    /// place that can hold one — so applying a filter in an aside leaves the aside standing,
    /// the same reason it never reports unsaved changes. Untracked answers that question and
    /// only that one: what a DIALOG does with it is decided below.</summary>
    [Fact]
    public async Task AnUntrackedFormInAnAside_NeverCloses()
    {
        var host = RenderInAside(p => p.Add(x => x.Untracked, true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Contains("aside=", Navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>A host-managed surface, the way NsOpenDialog builds one: no route in the
    /// address, a Close override its host owns — so "the dialog closed" is a fact about that
    /// override having been called.</summary>
    IRenderedComponent<SurfaceFormHost> RenderInDialog(
        Action close,
        Action<ComponentParameterCollectionBuilder<SurfaceFormHost>>? extra = null)
    {
        return Render<SurfaceFormHost>(p =>
        {
            p.Add(x => x.RouteTable, BuildRouteTable());
            p.Add(x => x.Model, new SubmitTrackingModel());
            p.Add(x => x.Name, Surfaces.Dialog);
            p.Add(x => x.Untracked, true);
            p.Add(x => x.Close, close);
            extra?.Invoke(p);
        });
    }

    /// <summary>The story `Untracked` used to confuse (issue #46): a dialog form declares it to
    /// opt out of change TRACKING — a password, an authorization, one address collected in
    /// memory is an act, not a draft — and used to lose the auto-close with it, so ten dialogs
    /// called Surface.Close() by hand at the tail of their own handler. The two meanings are
    /// separate now: a dialog is opened to ask exactly one thing, so its act succeeding is what
    /// finishes it, tracked or not.</summary>
    [Fact]
    public async Task AnUntrackedFormInADialog_ClosesOnASuccessfulSubmit()
    {
        var closes = 0;

        var host = RenderInDialog(() => closes++);

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(1, closes);
    }

    /// <summary>An untracked form marks the surface nothing, so the dirty flag cannot be what
    /// answers here: the form's own refusal is. The message it just drew is the one answer it
    /// is not allowed to lose.</summary>
    [Fact]
    public async Task AnUntrackedFormInADialog_TheServerRefused_StaysOpenAndDrawsTheMessage()
    {
        var closes = 0;

        var problem = new Problem("RuleViolation", "The operation could not be completed",
            [new Issue("RuleViolation", "El eje va de 1 a 180 grados.", "AxisRange")], 422);

        var host = RenderInDialog(() => closes++, p => p.Add(x => x.Problem, problem));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(0, closes);
        Assert.Contains("El eje va de 1 a 180 grados.", host.Find(".ns-form-problem").TextContent, StringComparison.Ordinal);
    }

    /// <summary>The trap the hand wiring hid: LocationForm returns without saving when no type
    /// was chosen, and nothing was refused and nothing failed — a naive auto-close would read
    /// that return as a save and close over the address the user never entered. A handler that
    /// gives up says so (Abort), and an aborted submit is not a success.</summary>
    [Fact]
    public async Task AnUntrackedFormInADialog_WhoseHandlerAborted_StaysOpen()
    {
        var closes = 0;

        var host = RenderInDialog(() => closes++, p => p.Add(x => x.Aborts, true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(0, closes);
    }

    /// <summary>Abort is about the submit, not about tracking: a tracked form in an aside that
    /// gives up keeps its surface too.</summary>
    [Fact]
    public async Task ATrackedSubmitTheHandlerAborted_KeepsTheAsideOpen()
    {
        var host = RenderInAside(p => p.Add(x => x.Aborts, true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Contains("aside=", Navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>The one exception the survey founded: a multi-add loop (the quick journal entry)
    /// files a document and offers the next one on the same surface. It is a page's own
    /// declaration, never a guess about which flows want it — the default is CLOSE.</summary>
    [Fact]
    public async Task AFormDeclaringKeepOpen_StaysOpenAfterASuccessfulSubmit()
    {
        var host = RenderInAside(p => p.Add(x => x.KeepOpen, true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Contains("aside=", Navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>A host-managed surface has no route in the address: it closes through the
    /// override NsOpenDialog hands it, and the rule reaches it the same way.</summary>
    [Fact]
    public async Task ASuccessfulSubmitInAHostManagedDialog_ClosesItThroughItsHost()
    {
        var closed = 0;

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Name, Surfaces.Dialog)
            .Add(x => x.Close, () => closed++));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(1, closed);
    }

    /// <summary>nsail#1420: the receta filled on its Lejos/Cerca tab saved and left its aside
    /// standing. Nothing about the receta was special — EyesEditor writes values the EditContext
    /// never sees, so it reports the SURFACE itself (SetDirty(this)), and CreatePrescriptionPage
    /// never spent that report. HasChanges stayed true over a document that had just been saved
    /// and Finished refused to close. Every collection editor in the tree is that shape, so the
    /// close it made conditional was the Stack's rule, not one page's: a tracked submit spends
    /// every report on its surface, and no page has a ClearDirty left to forget.</summary>
    [Fact]
    public async Task ASubmitOnASurfaceAnEditorBesideTheFormMarkedDirty_ClosesItAllTheSame()
    {
        var host = RenderInAside(p => p.Add(x => x.NavigatesTo, "directory/parties/7/edit"));

        await host.InvokeAsync(() => host.Instance.Probe!.MarkDirty());

        Assert.True(host.Instance.Surface!.HasChanges);

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.DoesNotContain("aside=", Navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>The other half of the same sentence: the marker is SPENT, not merely outvoted.
    /// A surface outlives the page rendered in it, so a report left standing asks the next
    /// screen about changes nobody made (CreateClearDirtyTests, Directory).</summary>
    [Fact]
    public async Task ASuccessfulSubmit_SpendsEveryReportOfUnsavedChangesOnItsSurface()
    {
        var navigation = Navigation;

        navigation.NavigateTo("/directory/parties/new");
        _locations.Clear();

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.NavigatesTo, "directory/parties/7/edit"));

        await host.InvokeAsync(() => host.Instance.Probe!.MarkDirty());

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.False(host.Instance.Surface!.HasChanges);
    }

    /// <summary>The bound on the sentence above: a surface can hold two documents — the settings
    /// page whose credentials and whose quota are saved by their own buttons — and saving one is
    /// not saving the other. Only the editors beside a form are its to spend; another form
    /// speaks for itself, or its Guardar would grey out over values somebody just typed.</summary>
    [Fact]
    public async Task ASubmit_LeavesAnotherFormOnTheSameSurfaceHoldingItsOwnChanges()
    {
        var navigation = Navigation;

        navigation.NavigateTo("/directory/parties/new");
        _locations.Clear();

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Sibling, true));

        await host.InvokeAsync(() => host.FindAll("input[type=text]")[1].Change("Marisa"));

        Assert.True(host.Instance.Surface!.HasChanges);

        await host.InvokeAsync(() => host.FindAll("form")[0].Submit());

        Assert.True(host.Instance.Surface!.HasChanges);
    }

    /// <summary>The same bound, one step later: the window a submit holds its own echoes in
    /// (nsail#1450) reaches exactly what the spend reaches, so the second form reporting itself
    /// WHILE the first one saves is neither held nor dropped. A person can be typing into it —
    /// only the saving form's own fields freeze — and its Guardar has to stay live.</summary>
    [Fact]
    public async Task ASubmit_LeavesAnotherFormThatReportedWhileItWasInFlightHoldingItsChanges()
    {
        var navigation = Navigation;

        navigation.NavigateTo("/directory/parties/new");
        _locations.Clear();

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Sibling, true)
            .Add(x => x.SiblingReportsWhileSaving, true)
            .Add(x => x.NavigatesTo, "directory/parties/7/edit"));

        await host.InvokeAsync(() => host.FindAll("form")[0].Submit());

        Assert.True(host.Instance.Surface!.HasChanges);
    }

    /// <summary>The window belongs to the submit that opened it, and a sibling's submit landing
    /// inside this one is the shipped shape that says so: a settings page draws a second Guardar
    /// and an untracked Probar, and nothing disables either while the first form saves. Refused,
    /// this submit still owes back the report its own window held — a sibling that closed it
    /// would leave the surface CLEANER than the submit found it, which greys out Guardar and
    /// silences the exit guard over an edit nobody saved.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARefusedSubmit_PutsBackWhatItsWindowHeld_ThoughASiblingSubmittedInsideIt(bool untracked)
    {
        var navigation = Navigation;

        navigation.NavigateTo("/directory/parties/new");
        _locations.Clear();

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Sibling, true)
            .Add(x => x.SiblingUntracked, untracked)
            .Add(x => x.ReportsWhileSaving, true)
            .Add(x => x.Aborts, true)
            .Add(x => x.WhileSaving, async self =>
            {
                await self.SiblingForm!.Submit();
            }));

        var surface = host.Instance.Surface!;

        await host.InvokeAsync(() => host.FindAll("form")[0].Submit());

        Assert.True(surface.HasChanges);

        // Asked for by name: the aggregate reads dirty for whichever source still holds a
        // report, and the one this fact is about is the editor's.
        await host.InvokeAsync(() => host.Instance.Probe!.ClearDirty());

        Assert.False(surface.HasChanges);
    }

    /// <summary>Two documents saving at once on one surface, each answering for its own reports:
    /// the sibling's save took, so the report somebody typed into it is spent and no refusal of
    /// the document beside it hands that back; this one was refused, so the echo its own window
    /// held comes back whole.</summary>
    [Fact]
    public async Task TwoSubmitsInFlightOnOneSurface_EachAnswerForTheirOwnReports()
    {
        var navigation = Navigation;

        navigation.NavigateTo("/directory/parties/new");
        _locations.Clear();

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Sibling, true)
            .Add(x => x.ReportsWhileSaving, true)
            .Add(x => x.Aborts, true)
            .Add(x => x.WhileSaving, async self =>
            {
                await self.SiblingForm!.Submit();
            }));

        var surface = host.Instance.Surface!;

        await host.InvokeAsync(() => host.FindAll("input[type=text]")[1].Change("Marisa"));

        Assert.True(surface.HasChanges);

        await host.InvokeAsync(() => host.FindAll("form")[0].Submit());

        Assert.True(surface.HasChanges);

        // Drop the echo and nothing is left: the sibling's own report went with its own save.
        await host.InvokeAsync(() => host.Instance.Probe!.ClearDirty());

        Assert.False(surface.HasChanges);
    }

    /// <summary>A filter or a search holds no document, so its submit speaks for nobody but
    /// itself: the report an editor made about the document standing beside it on the same
    /// surface is not the filter's to spend.</summary>
    [Fact]
    public async Task AnUntrackedSubmit_LeavesTheDocumentsOwnReportStanding()
    {
        var host = RenderInAside(p => p.Add(x => x.Untracked, true));

        await host.InvokeAsync(() => host.Instance.Probe!.MarkDirty());

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.True(host.Instance.Surface!.HasChanges);
    }

    /// <summary>The other side of spending a report: a submit that ends refused saved nothing,
    /// so it owes back every report it took — the editors' as much as the form's own. A report
    /// left spent greys out Guardar (NsSubmit gates on HasChanges) and silences the
    /// navigation-away guard over rows the user typed and nobody stored.</summary>
    [Fact]
    public async Task ARefusedSubmit_PutsBackTheEditorsReportOfUnsavedChanges()
    {
        var problem = new Problem("RuleViolation", "The operation could not be completed",
            [new Issue("RuleViolation", "El eje va de 1 a 180 grados.", "AxisRange")], 422);

        var host = RenderInAside(p => p.Add(x => x.Problem, problem));
        var surface = host.Instance.Surface!;

        await host.InvokeAsync(() => host.Instance.Probe!.MarkDirty());

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.True(surface.HasChanges);
        Assert.False(SubmitDisabled(host));
        Assert.Contains("aside=", Navigation.Uri, StringComparison.Ordinal);

        // The aggregate cannot answer this door on its own: ApplyProblem re-marks the FORM, so
        // the surface reads dirty whether or not the editor got its report back. Dropping the
        // form's is what asks the question this fact is about.
        var form = host.FindComponent<NsForm<SubmitTrackingModel>>().Instance;

        await host.InvokeAsync(() => surface.SetUnchanged(form));

        Assert.True(surface.HasChanges);
    }

    /// <summary>The door with no Problem behind it, and the one that made this worth pinning:
    /// Abort() reaches ApplyProblem never — it is the refusal with nothing to say — so the
    /// restore cannot live there. The surface is left exactly as dirty as the submit found
    /// it.</summary>
    [Fact]
    public async Task ASubmitTheHandlerAborted_PutsBackTheEditorsReportOfUnsavedChanges()
    {
        var host = RenderInAside(p => p.Add(x => x.Aborts, true));

        await host.InvokeAsync(() => host.Instance.Probe!.MarkDirty());

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.True(host.Instance.Surface!.HasChanges);
        Assert.False(SubmitDisabled(host));
        Assert.Contains("aside=", Navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>Exactly as dirty as it found it cuts both ways: an abort over a document nobody
    /// edited restores nothing, or the guard would challenge a departure from a clean page.
    /// </summary>
    [Fact]
    public async Task AnAbortedSubmitNobodyEditedBefore_LeavesTheSurfaceClean()
    {
        var host = RenderInAside(p => p.Add(x => x.Aborts, true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.False(host.Instance.Surface!.HasChanges);
    }

    /// <summary>The reports go BACK, they are not recomputed: an editor that reported while the
    /// submit was in flight is holding an edit of its own, and the restore adds to it rather
    /// than writing over it.</summary>
    [Fact]
    public async Task ARefusedSubmit_KeepsAReportTheEditorMadeWhileItWasInFlight()
    {
        var host = RenderInAside(p => p
            .Add(x => x.Aborts, true)
            .Add(x => x.ReportsWhileSaving, true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.True(host.Instance.Surface!.HasChanges);
    }

    static bool SubmitDisabled(IRenderedComponent<SurfaceFormHost> host)
    {
        return host.Find("button[type=submit]").HasAttribute("disabled");
    }

    /// <summary>The 212e2a0f guard must not challenge the departure it was the save that caused:
    /// the form is clean by the time the surface closes, so no confirm can fire. Edited through
    /// a real DOM event first, so the surface is dirty the way a person makes it dirty.</summary>
    [Fact]
    public async Task ClosingOnSave_NeverAsksAboutUnsavedChanges()
    {
        var host = RenderInAside(p => p.Add(x => x.NavigatesTo, "directory/parties/7/edit"));

        await host.InvokeAsync(() => host.Find("input[type=text]").Change("Marisa"));

        Assert.True(host.Instance.Surface!.HasChanges);

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.DoesNotContain("aside=", Navigation.Uri, StringComparison.Ordinal);
    }
}
