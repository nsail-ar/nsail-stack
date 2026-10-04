// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
