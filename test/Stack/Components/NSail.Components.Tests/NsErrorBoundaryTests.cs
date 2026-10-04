// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The story: an unhandled render/load error (a 500 SenderNotFound reaching a card,
/// the story's own example) used to surface as the raw Problem JSON. NsErrorBoundary wraps
/// Blazor's own ErrorBoundary and renders NsPageError instead — the same splash-faced chrome
/// NsPageNotFound already wears — and recovers when the user navigates away, so a crash on one
/// page never haunts the next. HomeTestPage (RouteTableTests.cs, "/") stands in for HomePage.</summary>
public sealed class NsErrorBoundaryTests : BunitContext
{
    const string LogoUrl = "/probe/logo.svg";

    bool _throw = true;

    public NsErrorBoundaryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();

        // Same seam PageNotFoundTests already proved for NsPageNotFound: NsPageError resolves
        // its own RouteTable from DI, and the one AddRouteTable builds is the entry assembly's.
        Services.AddSingleton(new RouteTable(typeof(HomeTestPage).Assembly, []));

        AddAuthorization().SetAuthorized("probe");
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    // A RenderFragment (not a fixed markup string) so a later render of the SAME child picks
    // up whatever _throw reads at that moment — the mechanism the recovery test needs: the
    // boundary must remount ChildContent, not just replay whatever it drew the first time.
    RenderFragment ChildFragment => builder =>
    {
        builder.OpenComponent<ThrowingComponent>(0);
        builder.AddAttribute(1, nameof(ThrowingComponent.Throw), _throw);
        builder.CloseComponent();
    };

    IRenderedComponent<NsErrorBoundary> RenderBoundary()
    {
        return Render<NsErrorBoundary>(p => p
            .Add(x => x.LogoUrl, LogoUrl)
            .Add(x => x.HomePage, typeof(HomeTestPage))
            .Add(x => x.ChildContent, ChildFragment));
    }

    [Fact]
    public void AChildThatThrows_RendersTheErrorFaceInsteadOfTheRawException()
    {
        var boundary = RenderBoundary();

        Assert.NotEmpty(boundary.FindAll("#ns-page-error"));
        Assert.NotEmpty(boundary.FindAll(".ns-remote-image"));
        // The catalog is empty in this fixture set, as PageNotFoundTests' own comment notes --
        // StringManager's documented fallback is the key itself, so this proves the message
        // actually asks for Common.UnhandledError rather than some other text.
        Assert.Equal("Common.UnhandledError", boundary.Find("#ns-page-error h6").TextContent);

        var home = boundary.Find("a.ns-page-error-home");

        Assert.Equal("/", home.GetAttribute("href"));
        Assert.NotEmpty(boundary.FindAll(".ns-page-error-reload"));
        Assert.Empty(boundary.FindAll("#ns-probe-throwing-child"));
    }

    [Fact]
    public void NoError_RendersTheChildContentAndNotTheErrorFace()
    {
        _throw = false;

        var boundary = RenderBoundary();

        Assert.NotEmpty(boundary.FindAll("#ns-probe-throwing-child"));
        Assert.Empty(boundary.FindAll("#ns-page-error"));
    }

    [Fact]
    public void ANavigation_RecoversTheBoundaryInsteadOfStayingTrippedForever()
    {
        var boundary = RenderBoundary();

        Assert.NotEmpty(boundary.FindAll("#ns-page-error"));

        // The child that crashed the first render would crash it again on a bare re-render --
        // flipping the flag before navigating is what tells a passing assertion apart from a
        // Recover() that never ran.
        _throw = false;

        Navigation.NavigateTo("/probe/elsewhere");

        Assert.Empty(boundary.FindAll("#ns-page-error"));
        Assert.NotEmpty(boundary.FindAll("#ns-probe-throwing-child"));
    }

    // What NsPageError's Reload actually is: an anchor to the address the reader is already at,
    // which under a circuit is a SOFT navigation to the same URL. That is a weaker signal than
    // the test above -- the location does not change -- so the boundary's recovery is pinned on
    // it directly: without this, "Reload recovers" rests on nothing anybody watches.
    [Fact]
    public void ANavigationToTheSameAddress_RecoversTheBoundaryToo()
    {
        var boundary = RenderBoundary();

        Assert.NotEmpty(boundary.FindAll("#ns-page-error"));

        var reload = boundary.Find("a.ns-page-error-reload").GetAttribute("href");

        Assert.Equal(Navigation.Uri, reload);

        _throw = false;

        Navigation.NavigateTo(Navigation.Uri);

        Assert.Empty(boundary.FindAll("#ns-page-error"));
        Assert.NotEmpty(boundary.FindAll("#ns-probe-throwing-child"));
    }
}
