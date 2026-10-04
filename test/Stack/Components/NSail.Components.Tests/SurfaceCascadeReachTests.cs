// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Where the surface cascade has to reach: not a link wired directly under
/// NsSurfaceContext, but a link inside a routed page inside a layout inside the router — the
/// shape every real link in both products has. The href a link resolves is the whole feature:
/// a dropped surface target renders a working full-page navigation, so nothing about it is
/// visible until the address is read.</summary>
public sealed class SurfaceCascadeReachTests : BunitContext
{
    public SurfaceCascadeReachTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        AddAuthorization().SetAuthorized("probe");
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IRenderedComponent<NsRouter> RenderApp(string uri)
    {
        Navigation.NavigateTo(uri);

        return Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(ProbePage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));
    }

    /// <summary>The measured symptom: the link on the page carries the page's own address plus
    /// the aside's query parameter, never the bare destination route.</summary>
    [Fact]
    public void ALinkTargetingTheAside_CarriesTheAsideQueryParameter()
    {
        var app = RenderApp("/probe");

        Assert.Equal(
            "/probe?aside=probe%2Fnew",
            app.Find("a.probe-aside").GetAttribute("href"));
    }

    /// <summary>The link next to it, with no target, leaves the page: same cascade, different
    /// answer, so a green pair rules out a cascade that is simply absent.</summary>
    [Fact]
    public void ALinkWithNoTargetOnTheMainSurface_IsTheBareRoute()
    {
        var app = RenderApp("/probe");

        Assert.Equal("/probe/other", app.Find("a.probe-plain").GetAttribute("href"));
    }

    /// <summary>The regression this story is about, reduced to its mechanism: a link that no
    /// cascade reaches. It is what a vendor portal renders (a dropdown, a menu, a dialog's own
    /// chrome are siblings of the router, not descendants), and it is what any client tree that
    /// loses the cascade produces. The target used to be dropped and the bare route handed back
    /// — a working full-page navigation, which is why nobody could see it. The root surface
    /// answers now, and it answers the same thing the page's own cascade would have.</summary>
    [Fact]
    public void ALinkTargetingTheAsideWithNoCascadeAtAll_StillCarriesTheAsideQueryParameter()
    {
        Navigation.NavigateTo("/probe");

        var link = Render<NsLink>(p => p
            .Add(x => x.Href, "probe/new")
            .Add(x => x.Target, Surfaces.Aside)
            .Add(x => x.Label, "Open"));

        Assert.Equal("/probe?aside=probe%2Fnew", link.Find("a").GetAttribute("href"));

        // The same absence used to cost the history rule too (be140214): with nothing to ask
        // whether the move stays in place, the click fell back to Blazor's own push.
        Assert.NotEmpty(link.FindAll("span.ns-link-intercept"));
    }

    /// <summary>Auto is the target the reported card carries, and it is the one that cannot be
    /// read off the link: it means "one surface further out than wherever I am", so it needs a
    /// surface to escalate FROM. With no cascade the root surface is the main one, and main
    /// escalates to the aside — the same answer the page's own cascade gives.</summary>
    [Fact]
    public void ALinkTargetingAutoWithNoCascadeAtAll_EscalatesToTheAside()
    {
        Navigation.NavigateTo("/probe");

        var link = Render<NsLink>(p => p
            .Add(x => x.Href, "probe/new")
            .Add(x => x.Target, Surfaces.Auto)
            .Add(x => x.Label, "Open"));

        Assert.Equal("/probe?aside=probe%2Fnew", link.Find("a").GetAttribute("href"));
    }

    /// <summary>Surfaces.Main is the one target whose bare route is the CORRECT answer, so it is
    /// the guard on the fix: making every link resolve must not turn a deliberate departure
    /// into a surface move.</summary>
    [Fact]
    public void ALinkTargetingMainWithNoCascadeAtAll_IsStillTheBareRoute()
    {
        Navigation.NavigateTo("/probe");

        var link = Render<NsLink>(p => p
            .Add(x => x.Href, "probe/other")
            .Add(x => x.Target, Surfaces.Main)
            .Add(x => x.Label, "Leave"));

        Assert.Equal("/probe/other", link.Find("a").GetAttribute("href"));
    }

    /// <summary>The discriminator the story asks for. This link declares NO target and renders
    /// INSIDE the aside. A live cascade answers with the aside's own query parameter; a dead
    /// one answers with the bare route — the identical string a lost Target produces on the
    /// page outside. Only this link tells the two failures apart.</summary>
    [Fact]
    public void ALinkInsideTheAsideWithNoTarget_NavigatesTheAsideItself()
    {
        var app = RenderApp("/probe?aside=probe%2Fnew");

        Assert.Equal(
            "/probe?aside=probe%2Fother",
            app.Find("a.probe-inside").GetAttribute("href"));
    }
}
