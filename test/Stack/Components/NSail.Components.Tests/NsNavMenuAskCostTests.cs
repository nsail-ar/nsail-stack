// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/askcost/parties")]
public sealed class AskCostPartiesPage : ComponentBase;

[Route("/askcost/access")]
public sealed class AskCostAccessPage : ComponentBase;

/// <summary>nsail#795: two thirds of a signed-in screen's API traffic was the menu. A
/// contributor is a kit's chance to ask its own server — the three sign-in kits each spend an
/// HTTP round trip deciding whether their Accesos leaf belongs — and the drawer was rebuilding
/// the whole tree on every parameter set. A RenderFragment parameter never compares equal to
/// itself, so every render of whatever hosts the drawer arrives as one: sixteen host renders
/// measured 33 asks here (the initial build, then two per render — the parameters re-set and
/// the cascade re-notified), which is the shape of the 16 calls each to /api/apple/sign-in,
/// /api/google/sign-in and /api/meta/sign-in read off test.optical.nsail.ar. The unit is the
/// ask, and the number is one.</summary>
public sealed class NsNavMenuAskCostTests : BunitContext
{
    const int HostRenders = 16;

    readonly ProbeAuthenticationStateProvider _authentication = new();

    readonly CountingNavContributor _contributor = new(
        new NavMenuItem { Name = "Parties", PageType = typeof(AskCostPartiesPage) });

    public NsNavMenuAskCostTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuAskCostTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(_contributor);
        JSInterop.Mode = JSRuntimeMode.Loose;

        // A session that can actually flip, which bUnit's own authorization doubles cannot do
        // (testing.md): they answer from a context the test sets rather than from a provider,
        // and the flip is half of what is pinned here. Its placeholders are registered before
        // this constructor and AddAuthorizationCore only ever TryAdds, so they are removed.
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<AuthenticationStateProvider>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authentication);
    }

    IRenderedComponent<NavMenuHost> RenderHost()
    {
        return Render<NavMenuHost>(p => p.Add(x => x.Tick, 0));
    }

    static void ReRender(IRenderedComponent<NavMenuHost> host, int times)
    {
        for (var tick = 1; tick <= times; tick++)
        {
            var next = tick;

            host.Render(p => p.Add(x => x.Tick, next));
        }
    }

    /// <summary>The defect itself, in the only unit that matters: asks. Sixteen renders of the
    /// host used to cost 33 of them; one session is one ask.</summary>
    [Fact]
    public void TheMenuAsksEachContributorOnceHoweverOftenItsHostRenders()
    {
        var host = RenderHost();

        ReRender(host, HostRenders);

        Assert.Equal(1, _contributor.Calls);
    }

    /// <summary>The other half of the same claim: nothing was saved by rendering less. The
    /// entries the one ask produced are still on screen after every one of those renders.</summary>
    [Fact]
    public void TheEntriesSurviveEveryOneOfThoseRenders()
    {
        var host = RenderHost();

        ReRender(host, HostRenders);

        Assert.NotNull(host.Find("a[href='/askcost/parties']"));
    }

    /// <summary>And the memo is not a freeze: the session is the one thing the tree is a
    /// function of besides the contributor set, so a flip asks again — exactly once more.
    /// Without this the fix would hide a provider turned on for the session that just signed in.</summary>
    [Fact]
    public void ASessionChangeAsksAgain()
    {
        var host = RenderHost();

        ReRender(host, HostRenders);

        _authentication.SignOut();

        host.WaitForAssertion(() => Assert.Equal(2, _contributor.Calls));

        ReRender(host, HostRenders);

        Assert.Equal(2, _contributor.Calls);
    }

    /// <summary>A drawer built from scratch — what a navigation leaves behind, since the route
    /// gate blanks the tree while it answers and the layout under it is constructed again —
    /// costs nothing. This is "the answer survives navigation within one session" said in the
    /// only unit that matters, and it is the half a memo living in the component cannot buy.</summary>
    [Fact]
    public void ASecondDrawerAsksNothingTheFirstAlreadyAsked()
    {
        var first = RenderHost();

        ReRender(first, HostRenders);

        var second = RenderHost();

        ReRender(second, HostRenders);

        Assert.NotNull(second.Find("a[href='/askcost/parties']"));
        Assert.Equal(1, _contributor.Calls);
    }

    /// <summary>The once-ness made real rather than likely: every one of those host renders lands
    /// while the first ask is still in flight, which is the ordinary case for a contributor that
    /// goes to its server. A guard taken after the contributor loop instead of before the first
    /// await reports 33 here and passes every other case in this class.</summary>
    [Fact]
    public void ParameterSetsArrivingWhileTheFirstAskIsStillInFlightStartNoSecondOne()
    {
        var held = new HeldNavContributor(
            new NavMenuItem { Name = "Access", PageType = typeof(AskCostAccessPage) });

        Services.AddSingleton<INavMenuContributor>(held);

        var host = RenderHost();

        ReRender(host, HostRenders);

        Assert.Equal(1, held.Calls);

        held.Answer();

        host.WaitForAssertion(() => Assert.NotNull(host.Find("a[href='/askcost/access']")));

        Assert.Equal(1, held.Calls);
    }

    /// <summary>A contributor that answers with nothing — the shape the sign-in kits take when
    /// their install is dark, or when they could not ask at all — leaves every other kit's
    /// entries standing. That is what the guard inside those contributors buys, said from the
    /// menu's side.</summary>
    [Fact]
    public void AContributorThatAnswersNothingLeavesTheRestOfTheMenuStanding()
    {
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor());

        var host = RenderHost();

        ReRender(host, HostRenders);

        Assert.NotNull(host.Find("a[href='/askcost/parties']"));
    }
}
