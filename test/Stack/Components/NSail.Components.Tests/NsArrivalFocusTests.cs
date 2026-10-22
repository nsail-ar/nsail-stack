// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace NSail.Components.Tests;

/// <summary>nsail#2338: what NsArrivalFocus schedules on a navigation's arrival — the DOM
/// precedence between the marker and the fallback, the wait for a late render and the "already
/// focused" guard all run in ns.js (arrivalFocus), which there is no JS harness in the tree to
/// pin (NsFocusCandidateMarkTests' own note) — proven instead by the Optical E2E this story adds.
/// What stays on this side is what C# alone decides: that a genuine navigation calls the JS
/// layer exactly once, with the fallback the page declared, that a render the page caused for
/// its own reasons never calls it again, and that a surface written over the address the person
/// is already standing at is no arrival at all.</summary>
public sealed class NsArrivalFocusTests : BunitContext
{
    const string Signal = "nsapp.arrivalFocus";

    public NsArrivalFocusTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    IReadOnlyList<JSRuntimeInvocation> Calls => [.. JSInterop.Invocations[Signal]];

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    static RouteData NewArrival()
    {
        return new RouteData(typeof(NsArrivalFocus), new Dictionary<string, object?>());
    }

    [Fact]
    public void ANewArrival_CallsTheRouterLayerOnceWithTheFallback()
    {
        Render<NsArrivalFocus>(p => p
            .Add(x => x.RouteData, NewArrival())
            .Add(x => x.FallbackSelector, "h1"));

        Assert.Single(Calls);
        Assert.Equal("h1", Calls[0].Arguments[0]);
    }

    [Fact]
    public void ARerenderWithTheSameRouteData_NeverCallsTheLayerAgain()
    {
        var arrival = NewArrival();
        var cut = Render<NsArrivalFocus>(p => p.Add(x => x.RouteData, arrival));

        cut.Render(p => p.Add(x => x.RouteData, arrival));

        Assert.Single(Calls);
    }

    [Fact]
    public void ASecondArrival_CallsTheLayerAgain()
    {
        Navigation.NavigateTo("/first");

        var cut = Render<NsArrivalFocus>(p => p.Add(x => x.RouteData, NewArrival()));

        Navigation.NavigateTo("/second");
        cut.Render(p => p.Add(x => x.RouteData, NewArrival()));

        Assert.Equal(2, Calls.Count);
    }

    /// <summary>The router hands down a fresh RouteData on every location change, and a screen
    /// writing its own tab, filter or aside onto the address it is standing at is one of those
    /// (SurfaceContext.InPlace) — but nobody arrived anywhere, so the cursor stays where the
    /// hand left it. Firing here is what took the cursor off a tab strip mid-gesture and, on a
    /// phone, closed the keyboard over the box that wrote the query (Vigía, nsail#2338 round 1).</summary>
    [Fact]
    public void ASurfaceWrittenOverTheSameAddress_IsNoArrival()
    {
        Navigation.NavigateTo("/parties");

        var cut = Render<NsArrivalFocus>(p => p.Add(x => x.RouteData, NewArrival()));

        Navigation.NavigateTo("/parties?aside=parties%2Fnew");
        cut.Render(p => p.Add(x => x.RouteData, NewArrival()));

        Navigation.NavigateTo("/parties?tab=2");
        cut.Render(p => p.Add(x => x.RouteData, NewArrival()));

        Assert.Single(Calls);
    }

    /// <summary>A fragment is the other same-path move, and the shell's own skip link is made of
    /// one: taking the cursor back to the heading is exactly what the link was pressed to get
    /// away from (NsMainLayout).</summary>
    [Fact]
    public void AFragmentMoveOnTheSameAddress_IsNoArrival()
    {
        Navigation.NavigateTo("/parties");

        var cut = Render<NsArrivalFocus>(p => p.Add(x => x.RouteData, NewArrival()));

        Navigation.NavigateTo("/parties#ns-content");
        cut.Render(p => p.Add(x => x.RouteData, NewArrival()));

        Assert.Single(Calls);
    }

    /// <summary>The pair that rules out a gate that is simply stuck shut: the same in-place write
    /// followed by a real move calls the layer again, and the path it left behind is the one it
    /// compares against.</summary>
    [Fact]
    public void AMoveAfterAnInPlaceWrite_IsAnArrival()
    {
        Navigation.NavigateTo("/parties");

        var cut = Render<NsArrivalFocus>(p => p.Add(x => x.RouteData, NewArrival()));

        Navigation.NavigateTo("/parties?aside=parties%2Fnew");
        cut.Render(p => p.Add(x => x.RouteData, NewArrival()));

        Navigation.NavigateTo("/parties/3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        cut.Render(p => p.Add(x => x.RouteData, NewArrival()));

        Assert.Equal(2, Calls.Count);
    }

    [Fact]
    public void APageThatDeclaresNoFallback_StillReadsTheRouterDefault()
    {
        Render<NsArrivalFocus>(p => p.Add(x => x.RouteData, NewArrival()));

        Assert.Equal("h1", Calls[0].Arguments[0]);
    }
}
