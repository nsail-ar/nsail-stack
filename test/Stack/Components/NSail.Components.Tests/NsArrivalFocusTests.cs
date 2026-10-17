// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;

namespace NSail.Components.Tests;

/// <summary>nsail#2338: what NsArrivalFocus schedules on a navigation's arrival — the DOM
/// precedence between the marker and the fallback, the wait for a late render and the "already
/// focused" guard all run in ns.js (arrivalFocus), which there is no JS harness in the tree to
/// pin (NsFocusCandidateMarkTests' own note) — proven instead by the Optical E2E this story adds.
/// What stays on this side is what C# alone decides: that a genuine navigation calls the JS
/// layer exactly once, with the fallback the page declared, and that a render the page caused
/// for its own reasons never calls it again.</summary>
public sealed class NsArrivalFocusTests : BunitContext
{
    const string Signal = "nsapp.arrivalFocus";

    public NsArrivalFocusTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    IReadOnlyList<JSRuntimeInvocation> Calls => [.. JSInterop.Invocations[Signal]];

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
        var cut = Render<NsArrivalFocus>(p => p.Add(x => x.RouteData, NewArrival()));

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
