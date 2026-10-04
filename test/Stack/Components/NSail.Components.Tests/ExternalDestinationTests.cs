// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The four-builder hunt's actual mechanism, pinned on Windows. A link asked
/// Uri whether its destination was absolute, and took that as "outside this app". Uri answers
/// that question about the OS's file paths, not about a web app's routes: on every
/// Unix-flavoured runtime a leading slash IS an absolute file:// path, so on WASM — and on a
/// Linux server's prerender — every rooted route in the app was handed back raw, with no
/// surface query and no click interception, which is a working full-page navigation and
/// therefore a failure with nothing to see. Windows said false for "/x" and the same binary
/// disagreed with itself across the boundary.
///
/// Windows cannot reproduce the "/x" divergence, so these tests do not pretend to: they use
/// "//host/share", the rooted shape Uri calls absolute on Windows TOO. Same rule, same exit,
/// deterministic here — a rooted value is this app's, whatever Uri thinks of it.</summary>
public sealed class ExternalDestinationTests : BunitContext
{
    public ExternalDestinationTests()
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

    /// <summary>Nothing rooted is somebody else's, whatever Uri says about it. The second case
    /// is the one Uri calls absolute on this OS, and it is the whole point of the row.</summary>
    [Theory]
    [InlineData("/directory/parties/new")]
    [InlineData("//host/share")]
    [InlineData("/")]
    public void ARootedDestinationIsNeverExternal(string href)
    {
        Assert.False(SurfaceContext.IsExternal(href));
    }

    /// <summary>A scheme-less relative route is ours too — that is the ordinary case, and it
    /// answered correctly on both platforms even before the fix.</summary>
    [Fact]
    public void ASchemeLessRelativeRouteIsNotExternal()
    {
        Assert.False(SurfaceContext.IsExternal("directory/parties/new"));
    }

    /// <summary>What the predicate is actually FOR: a destination carrying its own scheme
    /// belongs to somebody else and no surface may rewrite it.</summary>
    [Theory]
    [InlineData("https://nsail.test/help")]
    [InlineData("mailto:someone@nsail.test")]
    [InlineData("tel:+541100000000")]
    public void ADestinationCarryingItsOwnSchemeIsExternal(string href)
    {
        Assert.True(SurfaceContext.IsExternal(href));
    }

    /// <summary>The regression itself, through the component that had it: a rooted route with a
    /// surface target used to leave by the external exit — raw href, no query, no interception
    /// — which is exactly the string the Architect read off the client anchor
    /// (`target:aside;cascade:main;href:/directory/parties/new;intercepts:False`).</summary>
    [Fact]
    public void ARootedRouteWithASurfaceTargetStillResolvesToTheSurface()
    {
        Navigation.NavigateTo("/probe");

        var link = Render<NsLink>(p => p
            .Add(x => x.Href, "//probe/new")
            .Add(x => x.Target, Surfaces.Aside)
            .Add(x => x.Label, "Open"));

        Assert.Equal("/probe?aside=probe%2Fnew", link.Find("a").GetAttribute("href"));
        Assert.NotEmpty(link.FindAll("span.ns-link-intercept"));
    }

    /// <summary>The guard on the fix: making rooted values internal must not capture a
    /// destination that really is outside the app. Its address survives untouched and it keeps
    /// Blazor's own click, target and all.</summary>
    [Fact]
    public void ADestinationWithItsOwnSchemeIsLeftAloneEvenWithASurfaceTarget()
    {
        Navigation.NavigateTo("/probe");

        var link = Render<NsLink>(p => p
            .Add(x => x.Href, "https://nsail.test/help")
            .Add(x => x.Target, Surfaces.Aside)
            .Add(x => x.Label, "Help"));

        Assert.Equal("https://nsail.test/help", link.Find("a").GetAttribute("href"));
        Assert.Empty(link.FindAll("span.ns-link-intercept"));
    }
}
