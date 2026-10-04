// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#639: NsRouteGate's own redirect corrects the address, it is not a place the
/// visitor stood -- the same claim nsail#583 already answered for NsNotAuthorized. A pushed
/// entry lets "back" re-ask for the page the gate just refused, the gate fires again, and the
/// visitor never leaves.</summary>
public sealed class NsRouteGateTests : BunitContext
{
    public NsRouteGateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IReadOnlyList<NavigationHistory> _historyAtSetup = [];

    void NavigateAndCaptureHistory(string uri)
    {
        Navigation.NavigateTo(uri);
        _historyAtSetup = [.. Navigation.History];
    }

    /// <summary>The claim itself, pinned on the gate directly rather than through a whole app
    /// tree: a gate that names a destination leaves exactly one new entry, and that entry
    /// replaced the one it landed on rather than pushing beside it.</summary>
    [Fact]
    public void TheRedirect_ReplacesTheAskedPageInHistoryRatherThanPushingOntoIt()
    {
        Services.AddSingleton<IRouteGate>(new RedirectingRouteGate(typeof(ProbePage), typeof(ProbeOtherPage)));

        NavigateAndCaptureHistory("/probe");

        Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbePage)));

        var redirect = Assert.Single(Navigation.History.Except(_historyAtSetup));

        Assert.True(redirect.Options.ReplaceHistoryEntry);
    }

    /// <summary>The path the fix must not touch: a gate answering null (FakeRouteGate's own
    /// shape) still lets the asked page stand -- no move at all, replaced or otherwise.</summary>
    [Fact]
    public void AGateAnsweringNull_NavigatesNowhere()
    {
        Services.AddSingleton<IRouteGate, FakeRouteGate>();

        NavigateAndCaptureHistory("/probe");

        Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbePage)));

        Assert.Empty(Navigation.History.Except(_historyAtSetup));
    }

    /// <summary>nsail#693: two gates with one screen each -- an install's wizard and Iam's
    /// sign-up form -- would otherwise send the visitor to the other's destination forever. The
    /// gate holding the page CLAIMS it (it answers the page it was asked about) and nothing
    /// lighter is asked, so the loop is unrepresentable rather than a registration order
    /// somebody has to remember.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheDecidingGatesClaim_HoldsThePage_WhicheverOrderTheGatesRegisterIn(bool claimantFirst)
    {
        var claimant = new ClosedRouteGate(typeof(ProbePage), weight: -10);
        var other = new ClosedRouteGate(typeof(ProbeOtherPage));

        Services.AddSingleton<IRouteGate>(claimantFirst ? claimant : other);
        Services.AddSingleton<IRouteGate>(claimantFirst ? other : claimant);

        NavigateAndCaptureHistory("/probe");

        Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbePage)));

        Assert.Empty(Navigation.History.Except(_historyAtSetup));
    }

    /// <summary>nsail#693, the hole the claim rule opened: a claimant used to silence every
    /// gate behind it, so a gate holding that same page for its OWN reason was never asked --
    /// a provisional visitor walked the install's wizard because the wizard claimed the route
    /// first. A claim is a fixed point, not a veto: the gate that weighs less is asked first,
    /// and its redirect stands over the heavier gate's claim.</summary>
    [Fact]
    public void AClaim_DoesNotSilenceAGateThatOutranksIt()
    {
        Services.AddSingleton<IRouteGate>(new ClosedRouteGate(typeof(ProbePage)));
        Services.AddSingleton<IRouteGate>(new ClosedRouteGate(typeof(ProbeOtherPage), weight: -10));

        NavigateAndCaptureHistory("/probe");

        Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbePage)));

        var redirect = Assert.Single(Navigation.History.Except(_historyAtSetup));

        Assert.Equal("/probe/other", redirect.Uri);
    }

    /// <summary>And the visitor stops there: the gate that outranked the claim claims its own
    /// destination, so the two closed gates settle on one screen instead of trading the
    /// visitor back.</summary>
    [Fact]
    public void TheOutrankingGatesOwnDestination_IsWhereTheVisitorStands()
    {
        Services.AddSingleton<IRouteGate>(new ClosedRouteGate(typeof(ProbePage)));
        Services.AddSingleton<IRouteGate>(new ClosedRouteGate(typeof(ProbeOtherPage), weight: -10));

        NavigateAndCaptureHistory("/probe/other");

        Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbeOtherPage)));

        Assert.Empty(Navigation.History.Except(_historyAtSetup));
    }

    /// <summary>Weight is what decides, and ties keep registration order -- the same rule
    /// every other contributed sequence sorts by, so a gate that asks for no precedence is
    /// asked where its host composed it.</summary>
    [Fact]
    public void AmongGatesOfEqualWeight_TheFirstRegisteredNamesTheDestination()
    {
        Services.AddSingleton<IRouteGate>(new RedirectingRouteGate(typeof(ProbeAdminPage), typeof(ProbePage)));
        Services.AddSingleton<IRouteGate>(new RedirectingRouteGate(typeof(ProbeAdminPage), typeof(ProbeOtherPage)));

        NavigateAndCaptureHistory("/probe/admin");

        Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbeAdminPage)));

        var redirect = Assert.Single(Navigation.History.Except(_historyAtSetup));

        Assert.Equal("/probe", redirect.Uri);
    }

    /// <summary>And Weight beats that order where it is stated: the lighter gate is asked
    /// first no matter which host registered which gate when.</summary>
    [Fact]
    public void TheLightestGate_NamesTheDestinationOverTheOneRegisteredFirst()
    {
        Services.AddSingleton<IRouteGate>(new RedirectingRouteGate(typeof(ProbeAdminPage), typeof(ProbePage)));
        Services.AddSingleton<IRouteGate>(new RedirectingRouteGate(typeof(ProbeAdminPage), typeof(ProbeOtherPage), weight: -10));

        NavigateAndCaptureHistory("/probe/admin");

        Render<NsRouteGate>(p => p
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.PageType, typeof(ProbeAdminPage)));

        var redirect = Assert.Single(Navigation.History.Except(_historyAtSetup));

        Assert.Equal("/probe/other", redirect.Uri);
    }
}
