// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/probe/dom")]
public sealed class DomProbeTestPage : ComponentBase;

/// <summary>Pins the instrument, not the feature. A link's resolution is invisible in its
/// output — a dropped target and a genuine departure render the same bare route — so the
/// reading has to be IN the DOM before anyone can measure the client. These tests say what
/// data-ns-probe must contain and that it survives every chrome, including the two that
/// render their anchor inside a vendor component. What they do NOT do is reproduce the
/// reported loss: bUnit renders on the same runtime the server prerender uses, and that pass
/// is already proven correct — every assertion below is green today and would have been green
/// before the story opened.</summary>
public sealed class NsLinkDomProbeTests : BunitContext
{
    public NsLinkDomProbeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsLinkDomProbeTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        AddAuthorization().SetAuthorized("probe");
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    // The flat rung's icon-only face pairs its button with a tooltip when it has a label, and
    // a tooltip is a popover the vendor will not render without a provider whose disposal this
    // harness cannot carry. The label is dropped there only: the tooltip wraps the same
    // MudIconButton with the same parameters, so the attribute path under test is identical.
    // Everywhere else a missing label would itself put the link in the icon-only state, which
    // is why iconOnly and the label move together.
    IRenderedComponent<NsLink> RenderLink(NsAs chrome, Surface? target, bool iconOnly = false)
    {
        Navigation.NavigateTo("/probe");

        return Render<NsLink>(p => p
            .Add(x => x.Href, "probe/new")
            .Add(x => x.Target, target)
            .Add(x => x.As, chrome)
            .Add(x => x.Icon, NsIcons.Add)
            .Add(x => x.Breakpoint, iconOnly ? NsBreakpoint.Never : NsBreakpoint.Sm)
            .Add(x => x.Label, iconOnly && chrome == NsAs.Inline ? null : "Open"));
    }

    static string ProbeOf(IRenderedComponent<NsLink> link)
    {
        return link.Find("a").GetAttribute("data-ns-probe")
            ?? throw new InvalidOperationException("The anchor carries no data-ns-probe.");
    }

    /// <summary>The instrument must not reach a user. RELEASE resolves the probe to null and
    /// Blazor omits an attribute whose value is null — this is the assertion that says so, and
    /// it is the one test in the file that runs in both configurations.</summary>
    [Fact]
    public void OnlyADebugBuildWritesTheProbe()
    {
        var anchor = RenderLink(NsAs.Inline, Surfaces.Aside).Find("a");

#if DEBUG
        Assert.True(anchor.HasAttribute("data-ns-probe"));
#else
        Assert.False(anchor.HasAttribute("data-ns-probe"));
#endif
    }

#if DEBUG

    /// <summary>The reading the next live measurement is made of: what the executing code saw
    /// (target, cascade, the surface that answered) beside what it produced (href), so a bare
    /// address can be attributed instead of guessed at.</summary>
    [Fact]
    public void TheAnchorCarriesWhatTheLinkResolvedAndWhatItResolvedItFrom()
    {
        var probe = ProbeOf(RenderLink(NsAs.Inline, Surfaces.Aside));

        Assert.Contains("target:aside", probe, StringComparison.Ordinal);
        Assert.Contains("cascade:null", probe, StringComparison.Ordinal);
        Assert.Contains("surface:main", probe, StringComparison.Ordinal);
        Assert.Contains("raw:probe/new", probe, StringComparison.Ordinal);
        Assert.Contains("href:/probe?aside=probe%2Fnew", probe, StringComparison.Ordinal);
        Assert.Contains("intercepts:True", probe, StringComparison.Ordinal);
    }

    /// <summary>The one bit no other evidence can supply: which pass rendered this anchor. The
    /// prerender and the interactive island produce the same markup shape, and the whole
    /// contradiction under investigation is that they disagree about the address.</summary>
    [Fact]
    public void TheProbeNamesTheRuntimeThatRenderedIt()
    {
        Assert.Contains("rt:server", ProbeOf(RenderLink(NsAs.Inline, Surfaces.Aside)), StringComparison.Ordinal);
    }

    /// <summary>A link with no target reads target:null — the same string a link that LOST its
    /// target would read. That is deliberate: the probe reports what the code saw, and the two
    /// cases are told apart by the page's own markup, not by the instrument.</summary>
    [Fact]
    public void ALinkThatWasGivenNoTargetSaysSo()
    {
        var probe = ProbeOf(RenderLink(NsAs.Inline, target: null));

        Assert.Contains("target:null", probe, StringComparison.Ordinal);
        Assert.Contains("href:/probe/new", probe, StringComparison.Ordinal);
    }

    /// <summary>Every rank but the flat one's anchor renders inside a vendor component, which
    /// is exactly why the intercept wrapper had to move out of the anchor in the first place.
    /// If the attribute did not splat through, the reported link — a Primary "Nuevo" — would be
    /// the one link the instrument could not read. Icon-only is a state, not a rank, so each
    /// rank is read in both.</summary>
    [Theory]
    [InlineData(NsAs.Inline, false)]
    [InlineData(NsAs.Inline, true)]
    [InlineData(NsAs.Main, false)]
    [InlineData(NsAs.Main, true)]
    [InlineData(NsAs.Default, false)]
    [InlineData(NsAs.Default, true)]
    [InlineData(NsAs.Important, false)]
    [InlineData(NsAs.Important, true)]
    public void EveryRankCarriesTheProbeOnItsAnchor(NsAs rank, bool iconOnly)
    {
        Assert.Contains(
            "target:aside",
            ProbeOf(RenderLink(rank, Surfaces.Aside, iconOnly)),
            StringComparison.Ordinal);
    }

    /// <summary>NsLink can only report the target it RECEIVED, so the hop above it needs its
    /// own mark: the two failures are identical from the anchor's side otherwise.</summary>
    [Fact]
    public void APageLinkMarksTheTargetItHandedDown()
    {
        Navigation.NavigateTo("/probe");

        var cut = Render<NsPageLink<DomProbeTestPage>>(p => p
            .Add(x => x.Target, Surfaces.Aside)
            .Add(x => x.Label, "Open"));

        var anchor = cut.Find("a");

        Assert.Contains("ns-probe-pagelink-aside", anchor.GetAttribute("class"), StringComparison.Ordinal);
        Assert.Contains("target:aside", anchor.GetAttribute("data-ns-probe")!, StringComparison.Ordinal);
    }

    /// <summary>The Print button of a comprobante, as the browser receives it: target="_blank"
    /// and an href that is the PDF's own route, from a page rendering in an aside. Resolving it
    /// against the aside instead handed the new tab this same page's address with the API route
    /// as its ?aside= value — the app booting again, which is the reload the story reports and
    /// a PDF nobody ever requested (nsail#584).</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("aside")]
    public void ABrowserTargetOpensTheRouteItself(string? surfaceName)
    {
        Navigation.NavigateTo("/accounting/vouchers?aside=accounting%2Fvouchers%2F1");

        var surface = new SurfaceContext(
            surfaceName is null ? null : new Surface(surfaceName),
            Navigation,
            Services.GetRequiredService<RouteTable>(),
            Services.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
            Services.GetRequiredService<SurfaceHistory>());

        var anchor = Render<NsLink>(p => p
            .AddCascadingValue(surface)
            .Add(x => x.Href, "api/accounting/vouchers/1/pdf")
            .Add(x => x.Target, Surfaces.Blank)
            .Add(x => x.Label, "Imprimir")).Find("a");

        Assert.Equal("/api/accounting/vouchers/1/pdf", anchor.GetAttribute("href"));
        Assert.Equal("_blank", anchor.GetAttribute("target"));
    }

#endif
}
