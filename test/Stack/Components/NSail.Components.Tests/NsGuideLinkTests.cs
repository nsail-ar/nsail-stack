// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#929: the door to the guide. A button, not a popover — NsHelp stays a
/// field's own two-line whisper — that opens the guide at the topic the screen names, on the
/// surface one step out from wherever it renders, and never in a browser tab.</summary>
public sealed class NsGuideLinkTests : BunitContext, IAsyncLifetime
{
    readonly BunitAuthorizationContext _auth;

    public NsGuideLinkTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Guide"] = "Ayuda",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(ProbeGuidePage).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddScoped<PageGate>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _auth = this.AddAuthorization();
        _auth.SetAuthorized("someone");
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // MudTooltip is a popover host, and bUnit's synchronous teardown cannot dispose the
    // MudBlazor service behind it.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    void WithGuide()
    {
        Services.AddScoped(provider => new GuideRoute(
            typeof(ProbeGuidePage),
            provider.GetRequiredService<RouteTable>()));
    }

    void WithRestrictedGuide()
    {
        Services.AddScoped(provider => new GuideRoute(
            typeof(RestrictedProbeGuidePage),
            provider.GetRequiredService<RouteTable>()));
    }

    IRenderedComponent<NsGuideLink> Show(string topic, Action<ComponentParameterCollectionBuilder<NsGuideLink>>? more = null)
    {
        return Render<NsGuideLink>(parameters =>
        {
            parameters.Add(guide => guide.Topic, topic);
            more?.Invoke(parameters);
        });
    }

    [Fact]
    public void ATopicOpensTheChapterItNames()
    {
        WithGuide();

        // Target is Auto, so from the main surface the guide opens on the aside: the route
        // travels as the surface's query value and the anchor stays the document's own.
        Assert.Equal(
            "/?aside=guide%2Fwork-orders",
            Show("work-orders").Find("a").GetAttribute("href"));
    }

    [Fact]
    public void ATopicWithASectionLandsOnThatSection()
    {
        WithGuide();

        Assert.Equal(
            "/?aside=guide%2Fwork-orders#cobrar-una-ot",
            Show("work-orders#cobrar-una-ot").Find("a").GetAttribute("href"));
    }

    // The guide is a place beside the screen, never a tab: a half-filled form underneath must
    // still be there when the reader is done.
    [Fact]
    public void TheGuideOpensBesideTheScreenAndNotInABrowserTab()
    {
        WithGuide();

        var cut = Show("work-orders");

        Assert.Null(cut.Find("a").GetAttribute("target"));
        Assert.Single(cut.FindAll(".ns-link-intercept a"));
    }

    [Fact]
    public void TheGlyphAloneIsTheDefaultAndItCarriesItsWord()
    {
        WithGuide();

        var cut = Show("work-orders");

        Assert.Equal("Ayuda", cut.Find("a").GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll(".ns-button-label"));
    }

    // nsail#1006: the ruling that drew Guide's own bare question mark distinct from Help's
    // circled one is retired — one glyph for both, so the guide door reads as the same family
    // as a field's own help.
    [Fact]
    public void TheGlyphIsTheSameCircledQuestionAFieldsHelpUses()
    {
        WithGuide();

        var cut = Show("work-orders");

        var path = cut.Find("a svg path");

        Assert.Contains(path.GetAttribute("d")!, NsIcons.Help.Markup);
    }

    // nsail#1006: the door sits last in the title row, whose Inline face is the vendor's own
    // icon button — the marker ns-mud.css hooks to cancel that button's own padding so the
    // drawn glyph, not its hit box, is what lines up with the toolbar's own trailing edge.
    [Fact]
    public void TheAnchorCarriesTheEdgeMarkerForTheStylesheetToCompensate()
    {
        WithGuide();

        var cut = Show("work-orders");

        Assert.Contains("ns-guide-link", cut.Find("a").ClassList);
    }

    [Fact]
    public void AScreenThatWantsTheWordAsksForItAndMayOverrideIt()
    {
        WithGuide();

        var cut = Show("onboarding", parameters => parameters
            .Add(guide => guide.Breakpoint, NsBreakpoint.Always)
            .Add(guide => guide.Label, "Instrucciones"));

        // The rank does not move with the word (nsail#925): a door to the guide is a title
        // bar's utility at every width, so the worded face is that rung's own text link.
        Assert.Equal("Instrucciones", cut.Find("a").TextContent);
    }

    // An app with no guide screen has no door to draw, and a screen that asked for one is
    // not a screen to break.
    [Fact]
    public void NothingIsDrawnWhereTheAppRegisteredNoGuidePage()
    {
        Assert.Empty(Show("work-orders").FindAll("a"));
    }

    // nsail#1797: the guide page's own [Authorize<T>] is the lever, so a session the gate
    // refuses gets no door either — the dead link NsGuideLink's own comment says it exists to
    // avoid, the other way a room can be closed.
    [Fact]
    public void NothingIsDrawnWhereTheSessionMayNotOpenTheGuidePage()
    {
        WithRestrictedGuide();

        Assert.Empty(Show("work-orders").FindAll("a"));
    }

    [Fact]
    public void TheDoorShowsWhereTheSessionMayOpenTheRestrictedGuidePage()
    {
        _auth.SetAuthorized("vendedor").SetRoles("employee");

        WithRestrictedGuide();

        Assert.NotEmpty(Show("work-orders").FindAll("a"));
    }
}
