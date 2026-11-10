// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The card half of the one-arrival rule. A dashboard card's read was resolved by the
/// prerender and painted into the document, and then sent again the moment the WebAssembly
/// client remounted the card — two round trips per card per page load, with the card's default
/// state rendered in between. What is pinned here: the prerender persists what it resolved, the
/// client adopts it without sending, the adoption costs no render at all, and a component that
/// did not opt in behaves exactly as it did.</summary>
public sealed class NsPartialHandoffTests : BunitContext
{
    const string Key = "HandoffProbeCard.ProbeRead";

    readonly CountingMediator _mediator = new(7);

    public NsPartialHandoffTests()
    {
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsPartialHandoffTests).Assembly, []));
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddSingleton<Mediator>(_mediator);
        Services.AddSingleton<MessageContextAccessor>();
    }

    [Fact]
    public async Task TheClientAdoptsTheAnswerThePrerenderResolved()
    {
        Services.AddSingleton((await Handoff.Carrying(Key, new ProbeAnswer { Count = 42 })).State);

        var cut = Render<HandoffProbeCard>();

        Assert.Equal(0, _mediator.Sends);
        Assert.Equal("42", cut.Find("#figure").TextContent);
    }

    /// <summary>The whole property: adopting is synchronous, so the client's first render is
    /// already the prerender's last one. A single render is what says no default state was
    /// painted before the figures.</summary>
    [Fact]
    public async Task TheAdoptedAnswerIsAlreadyOnTheFirstRender()
    {
        Services.AddSingleton((await Handoff.Carrying(Key, new ProbeAnswer { Count = 42 })).State);

        var cut = Render<HandoffProbeCard>();

        Assert.Equal(1, cut.RenderCount);
    }

    /// <summary>The other side of the same measurement — without a handoff the read costs the
    /// second render, which is the flash the card used to show on every page load.</summary>
    [Fact]
    public async Task WithNothingHandedOverTheReadRunsAndCostsARender()
    {
        Services.AddSingleton((await Handoff.Empty()).State);

        var cut = Render<HandoffProbeCard>();

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find("#figure").TextContent));

        Assert.Equal(1, _mediator.Sends);
        Assert.Equal(2, cut.RenderCount);
    }

    /// <summary>The server half. What the prerender persists has to be what the client above
    /// restores, key included, or the two ends drift apart in silence.</summary>
    [Fact]
    public async Task ThePrerenderPersistsWhatItResolved()
    {
        var handoff = await Handoff.Empty();

        Services.AddSingleton(handoff.State);

        var cut = Render<HandoffProbeCard>();

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find("#figure").TextContent));

        await handoff.Manager.PersistStateAsync(handoff, Renderer);

        Assert.Equal(7, handoff.Read<ProbeAnswer>(Key)?.Count);
    }

    /// <summary>One card drawn twice in one document is one key, and the state takes one
    /// write per key: the prerender persists it once rather than throwing on the second copy,
    /// which would empty every handoff of the page.</summary>
    [Fact]
    public async Task ACardDrawnTwicePersistsItsAnswerOnce()
    {
        var handoff = await Handoff.Empty();

        Services.AddSingleton(handoff.State);

        var cut = Render<HandoffProbePair>();

        cut.WaitForAssertion(() => Assert.Equal(["7", "7"], cut.FindAll("#figure").Select(figure => figure.TextContent)));

        await handoff.Manager.PersistStateAsync(handoff, Renderer);

        Assert.Equal(7, handoff.Read<ProbeAnswer>(Key)?.Count);
    }

    /// <summary>The client half: the key is taken once, and the second copy adopts the same
    /// answer instead of finding the key gone and reading again.</summary>
    [Fact]
    public async Task BothCopiesOfACardAdoptTheOneAnswer()
    {
        Services.AddSingleton((await Handoff.Carrying(Key, new ProbeAnswer { Count = 42 })).State);

        var cut = Render<HandoffProbePair>();

        Assert.Equal(0, _mediator.Sends);
        Assert.Equal(["42", "42"], cut.FindAll("#figure").Select(figure => figure.TextContent));
    }

    /// <summary>An instance adopts once: asking again under the same key — a Reload, a Retry, a
    /// saved event — is a re-read and sends, whatever another copy still holds.</summary>
    [Fact]
    public async Task ACardAskingAgainReadsForReal()
    {
        Services.AddSingleton((await Handoff.Carrying(Key, new ProbeAnswer { Count = 42 })).State);

        var cut = Render<HandoffProbeCard>();

        Assert.Equal("42", cut.Find("#figure").TextContent);

        cut.Render();

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find("#figure").TextContent));

        Assert.Equal(1, _mediator.Sends);
    }

    /// <summary>A copy that read again has outdated the arrival's answer: a copy mounted after it
    /// sends rather than adopting what its sibling already read past.</summary>
    [Fact]
    public async Task ACardMountedAfterACopyReReadSendsToo()
    {
        Services.AddSingleton((await Handoff.Carrying(Key, new ProbeAnswer { Count = 42 })).State);

        var pair = Render<HandoffProbePair>();
        var copy = pair.FindComponents<HandoffProbeCard>()[0];

        copy.Render();

        copy.WaitForAssertion(() => Assert.Equal("7", copy.Find("#figure").TextContent));

        var cut = Render<HandoffProbeCard>();

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find("#figure").TextContent));

        Assert.Equal(2, _mediator.Sends);
    }

    /// <summary>The shared answer lives as long as a copy holds it: a card mounted after every
    /// copy is gone reads afresh rather than adopting the arrival's answer.</summary>
    [Fact]
    public async Task ACardMountedAfterTheCopiesAreGoneReadsAgain()
    {
        Services.AddSingleton((await Handoff.Carrying(Key, new ProbeAnswer { Count = 42 })).State);

        Render<HandoffProbePair>();
        await DisposeComponentsAsync();

        var cut = Render<HandoffProbeCard>();

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find("#figure").TextContent));

        Assert.Equal(1, _mediator.Sends);
    }

    /// <summary>The subject is what tells two instances of one card type apart, so a card about
    /// a party is handed its own party's answer and nobody else's.</summary>
    [Fact]
    public async Task TheSubjectScopesTheKey()
    {
        var subject = Guid.NewGuid();
        var handoff = await Handoff.Empty();

        Services.AddSingleton(handoff.State);

        var cut = Render<HandoffProbeCard>(parameters => parameters.Add(card => card.Subject, subject));

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find("#figure").TextContent));

        await handoff.Manager.PersistStateAsync(handoff, Renderer);

        Assert.Null(handoff.Read<ProbeAnswer>(Key));
        Assert.Equal(7, handoff.Read<ProbeAnswer>($"{Key}:{subject}")?.Count);
    }

    /// <summary>Opt-in, and this is what that means: a hundred components inherit NsPartial and
    /// a plain Send is untouched by any of this, even with an answer sitting under its key.</summary>
    [Fact]
    public async Task AComponentThatDidNotOptInStillSends()
    {
        Services.AddSingleton((await Handoff.Carrying("SendProbeCard.ProbeRead", new ProbeAnswer { Count = 42 })).State);

        var cut = Render<SendProbeCard>();

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find("#figure").TextContent));

        Assert.Equal(1, _mediator.Sends);
    }

    /// <summary>A host with no handoff registered at all — a component test, a surface that is
    /// not a Blazor Web App — still renders the card rather than failing to construct it.</summary>
    [Fact]
    public void WithoutTheHandoffRegisteredTheCardStillReads()
    {
        var cut = Render<HandoffProbeCard>();

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find("#figure").TextContent));

        Assert.Equal(1, _mediator.Sends);
    }
}
