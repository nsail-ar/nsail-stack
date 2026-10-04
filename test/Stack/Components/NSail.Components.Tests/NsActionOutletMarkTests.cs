// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A contributed verb that has to ask something slow used to arrive after its row had
/// drawn and appear in it unannounced — Meta's Sincronizar on the templates grid (nsail#1060).
/// The outlet knows it is still asking, the presenter draws a mark in the slot that verb will
/// fill, and nothing on the page waits for any of it: the host's own verbs are there from the
/// first frame, and an outlet whose contributors already know shows no mark at all.</summary>
public sealed class NsActionOutletMarkTests : BunitContext, IAsyncLifetime
{
    public NsActionOutletMarkTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsActionOutletMarkTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddScoped<PageGate>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization().SetAuthorized("tester");
    }

    // MudPopoverProvider pulls in a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it (NsActionColumnTests' note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    WaitingContributor Waiting(string verb)
    {
        var contributor = new WaitingContributor { Verb = verb };

        Services.AddSingleton<IActionContributor<ProbeRowOutlet>>(contributor);

        return contributor;
    }

    void Settled(string verb)
    {
        Services.AddSingleton<IActionContributor<ProbeRowOutlet>>(new SettledContributor { Verb = verb });
    }

    IRenderedComponent<ActionOutletHost> Mount(string context = "Ana")
    {
        var cut = Render<ActionOutletHost>();

        cut.Render(p => p
            .Add(x => x.Mounted, true)
            .Add(x => x.Context, context)
            .Add(x => x.Leading, [new ActionItem { Name = "Edit", OnClick = () => Task.CompletedTask }])
            .Add(x => x.Constant, new ActionItem { Name = "Ficha", OnClick = () => Task.CompletedTask }));

        return cut;
    }

    // The cell in DOM order: every control by the label a screen reader reads, and the mark
    // where it stands among them.
    static IReadOnlyList<string> Strip(IRenderedComponent<ActionOutletHost> cut)
    {
        return cut
            .Find(".ns-action-toolbar")
            .QuerySelectorAll("button, .ns-action-mark")
            .Select(node => node.ClassList.Contains("ns-action-mark")
                ? "mark"
                : node.GetAttribute("aria-label") ?? "kebab")
            .ToList();
    }

    [Fact]
    public void TheSlotOfAContributorStillAnsweringCarriesTheMark()
    {
        var slow = Waiting("Sync");

        var cut = Mount();

        Assert.Equal(1, slow.Asks);
        Assert.Equal(["Edit", "mark", "Ficha"], Strip(cut));
    }

    /// <summary>AC1's second half and AC2: the page and the host's own verbs are already drawn
    /// while the contributor has answered nothing — the render that put them there did not
    /// wait for it.</summary>
    [Fact]
    public void ThePageAndTheHostsOwnVerbsDrawWithoutWaitingForAContribution()
    {
        var slow = Waiting("Sync");

        var cut = Mount();

        Assert.Equal(1, slow.Asks);
        Assert.Equal("Ana", cut.Find("#subject").TextContent);
        Assert.Equal(["Edit", "mark", "Ficha"], Strip(cut));
    }

    [Fact]
    public void TheMarkGivesWayToTheVerbItAnnounced()
    {
        var slow = Waiting("Sync");

        var cut = Mount();

        slow.Answer();

        cut.WaitForAssertion(() => Assert.Equal(["Edit", "Sync-Ana", "Ficha"], Strip(cut)));
    }

    /// <summary>AC3. One render is what says no mark was ever painted: the ask answered inside
    /// the same synchronous run, so the renderer had nothing to draw twice (the property
    /// NsPartial.Handoff is measured by, NsPartialHandoffTests).</summary>
    [Fact]
    public void AnOutletWhoseContributorsAnswerAtOnceShowsNoMark()
    {
        Settled("Sync");

        var cut = Mount();

        Assert.Equal(["Edit", "Sync-Ana", "Ficha"], Strip(cut));
        Assert.Equal(1, cut.FindComponent<ProbeRowOutlet>().RenderCount);
    }

    [Fact]
    public void AnOutletNobodyContributesToShowsNoMarkEither()
    {
        var cut = Mount();

        Assert.Equal(["Edit", "Ficha"], Strip(cut));
        Assert.Equal(1, cut.FindComponent<ProbeRowOutlet>().RenderCount);
    }

    /// <summary>Contributors stop queueing behind one another: both are asked before either has
    /// answered, so two slow ones cost the slower and not the sum.</summary>
    [Fact]
    public void EveryContributorIsAskedBeforeAnyOfThemAnswers()
    {
        var first = Waiting("First");
        var second = Waiting("Second");

        Mount();

        Assert.Equal(1, first.Asks);
        Assert.Equal(1, second.Asks);
    }

    /// <summary>Asked together, reported in contribution order all the same — and the slot is
    /// held until the last of them answers, never handed over piecemeal.</summary>
    [Fact]
    public void TheOrderIsTheContributionOrderAndNotTheAnsweringOrder()
    {
        var first = Waiting("First");
        var second = Waiting("Second");

        var cut = Mount();

        second.Answer();

        Assert.Equal(["Edit", "mark", "Ficha"], Strip(cut));

        first.Answer();

        cut.WaitForAssertion(() => Assert.Equal(["Edit", "First-Ana", "Second-Ana", "Ficha"], Strip(cut)));
    }

    /// <summary>A Context that moves while the first ask is in flight: the answer that arrives
    /// for the model that left is dropped, and the mark stays with the ask still running.</summary>
    [Fact]
    public void AnAnswerForTheModelThatLeftIsDroppedAndTheMarkStays()
    {
        var slow = Waiting("Sync");

        var cut = Mount("Ana");
        var outlet = cut.FindComponent<ProbeRowOutlet>();

        cut.Render(p => p.Add(x => x.Context, "Beto"));

        Assert.Equal(2, slow.Asks);

        // Counted after the Context moved, or the wait below would already be satisfied by the
        // render that moved it and would prove nothing about the answer.
        var drawn = outlet.RenderCount;

        slow.Answer(0);

        cut.WaitForState(() => outlet.RenderCount > drawn);

        Assert.Equal(["Edit", "mark", "Ficha"], Strip(cut));

        slow.Answer(1);

        cut.WaitForAssertion(() => Assert.Equal(["Edit", "Sync-Beto", "Ficha"], Strip(cut)));
    }
}
