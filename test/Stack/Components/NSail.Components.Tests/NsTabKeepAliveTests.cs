// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Proves the two behaviors NsTabs.KeepPanelsAlive is for: a validation problem on
/// a field that lives on an inactive tab still blocks submit, and a tab's content is not
/// torn down and rebuilt when the user switches away and back (EyesEditor.razor's distance
/// tab keeps state exactly this way).</summary>
public sealed class NsTabKeepAliveTests : BunitContext
{
    public NsTabKeepAliveTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, Fixtures.NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Rule one, both halves at once: the inactive tab's input is in the DOM (so it
    /// exists to EditContext) and the browser is told not to paint it. Measured against
    /// MudBlazor 9.10.0: the panel showing is the one marked .mud-tab-panel-active, and the
    /// hiding is the vendor's own stylesheet drawing every unmarked .mud-tab-panel as
    /// display:none — a rendered panel carries no style of its own, and NOT the
    /// .mud-tab-panel-hidden class the stylesheet also ships, which nothing on this path
    /// applies (see intentional-ui-cases.md). Hiding is the resting behaviour, not something a
    /// problem triggers.</summary>
    [Fact]
    public void InactiveTabsInput_IsInTheDomAndHiddenByCss()
    {
        var model = new TabFormModel { First = "ok", Second = "also ok" };

        var cut = Render<TabFormHost>(p => p.Add(x => x.Model, model));

        // Scoped to the panel container: MudBlazor puts "mud-tab-panel" on the header
        // buttons too, so a bare .mud-tab-panel selector matches four elements, not two.
        var panels = cut.FindAll(".mud-tabs-panels > .mud-tab-panel");
        Assert.Equal(2, panels.Count);

        // "Second" is the tab nobody selected, and its input is rendered all the same.
        var hidden = panels[1];
        Assert.Contains("value=\"also ok\"", hidden.OuterHtml);
        Assert.Single(hidden.QuerySelectorAll("input"));

        // ...and hidden by CSS rather than by not existing: the class it does NOT carry is
        // what a stylesheet rule of the vendor's draws as display:none, and no inline style
        // stands on either panel.
        Assert.DoesNotContain("mud-tab-panel-active", hidden.ClassList);
        Assert.Null(hidden.GetAttribute("style"));

        // The active panel is display:contents, not merely "not none": the wrapper gets out
        // of the way so NsTabs' flex column reaches the content that actually scrolls. It is
        // the class that says so, and .mud-tab-panel-hidden — which would beat it — is applied
        // to neither panel.
        Assert.Contains("mud-tab-panel-active", panels[0].ClassList);
        Assert.DoesNotContain("mud-tab-panel-hidden", panels[0].ClassList);
        Assert.DoesNotContain("mud-tab-panel-hidden", hidden.ClassList);
    }

    [Fact]
    public async Task ValidationErrorOnInactiveTab_IsInTheRenderedDomAndMarksItsTab()
    {
        var model = new TabFormModel { First = "ok", Second = null };

        var cut = Render<TabFormHost>(p => p.Add(x => x.Model, model));

        // A submit is what makes DataAnnotationsValidator run (EditContext.Validate raises
        // OnValidationRequested); only after that does the message exist to look for.
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        // "Second" lives on the tab that is not selected (index 0, "First", is active by
        // default). aria-invalid and the error text only render at all if the panel is
        // still mounted — under MudBlazor's own default (KeepPanelsAlive=false) the whole
        // subtree for a non-active tab is gone.
        var invalidInput = cut.Find("input[aria-invalid='true']");
        Assert.NotNull(invalidInput);

        // NsDataAnnotationsValidator speaks through Problems.Required, not the BCL's
        // "The Second field is required." — no StringManager entries are registered here,
        // so the key itself is what renders (the documented no-translation fallback), the
        // same convention NsFormRequiredTests uses for its untranslated PatientId case.
        Assert.Contains("Problems.Required", cut.Markup);

        // The problem-marker mechanism (NsTab deriving HasProblem from the fields it tracks):
        // the inactive "Second" tab gets a badge dot, the valid "First" tab does not.
        var tabHeaders = cut.FindAll(".mud-tab");
        Assert.Equal(2, tabHeaders.Count);
        Assert.DoesNotContain("mud-badge", tabHeaders[0].OuterHtml);
        Assert.Contains("mud-badge-dot", tabHeaders[1].OuterHtml);
        // mud-theme-error resolves --mud-palette-error, a CSS custom property MudBlazor's
        // ThemeProvider redefines per palette — the marker follows light/dark for free.
        Assert.Contains("mud-theme-error", tabHeaders[1].OuterHtml);
    }

    [Fact]
    public async Task ValidationErrorOnInactiveTab_BlocksSubmit()
    {
        var model = new TabFormModel { First = "ok", Second = null };
        var submitted = false;

        var cut = Render<TabFormHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);
    }

    [Fact]
    public async Task FixingTheInactiveTabField_UnblocksSubmit()
    {
        var model = new TabFormModel { First = "ok", Second = "filled" };
        var submitted = false;

        var cut = Render<TabFormHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
    }

    [Fact]
    public async Task SwitchingTabsAndBack_DoesNotReinitializeTheInactiveTabsContent()
    {
        var model = new TabFormModel();

        var cut = Render<TabFormHost>(p => p.Add(x => x.Model, model));

        // Every panel is mounted up front under KeepPanelsAlive, so the tracker on the
        // "Second" tab already ran once even though "First" is the active tab.
        Assert.Equal(1, cut.Instance.TrackerInitCount);

        var tabs = cut.FindAll(".mud-tab");
        Assert.Equal(2, tabs.Count);

        await cut.InvokeAsync(() => tabs[1].Click());
        cut.Render();
        tabs = cut.FindAll(".mud-tab");
        await cut.InvokeAsync(() => tabs[0].Click());
        cut.Render();

        // Had the panel been unmounted on the way out (KeepPanelsAlive=false, MudBlazor's
        // own default), coming back would run OnInitialized a second time.
        Assert.Equal(1, cut.Instance.TrackerInitCount);
    }
}
