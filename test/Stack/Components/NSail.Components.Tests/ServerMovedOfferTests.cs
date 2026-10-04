// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Builds;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Publishing;
using NSail.Metadata;

namespace NSail.Components.Tests;

// The screen half of nsail#1452. A deploy under an open tab is announced, never imposed: the
// offer waits above every page until the operator takes it, and taking it is what reloads.
// Nothing is said while the server is on the build this client booted with — a bar on every
// request of a developer's afternoon is the same defect wearing the opposite sign.
public sealed class ServerMovedOfferTests : BunitContext, IAsyncLifetime
{
    const string Message = "A new version is available.";
    const string Reload = "Reload";
    const string Elsewhere = "a-build-this-client-never-booted";

    readonly CountingDialogManager _dialogs = new();
    readonly ServerBuild _build = new();

    public ServerMovedOfferTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton<IBrandProvider>(new StaticBrandProvider(new Brand()));
        Services.AddSingleton<IThemeProvider>(new NullThemeProvider());
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.NewVersion"] = Message,
            ["Common.Reload"] = Reload,
        })]));
        Services.AddSingleton(new LanguageProvider { Current = "en" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<DialogManager>(_dialogs);
        Services.AddSingleton(_build);
        Services.AddScoped<PushFeed>();

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider, which NsSetup mounts, holds an async-only vendor service: tearing
    // down through IAsyncLifetime is what keeps BunitContext off its synchronous Dispose.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public async Task AServerOnAnotherBuildOffersAReload()
    {
        var cut = await Mounted();

        await Heard(cut, Elsewhere);

        var offer = Assert.Single(_dialogs.Offers);

        Assert.Equal(Message, offer.Message);
        Assert.Equal(Reload, offer.Label);
    }

    // Nothing moves until the offer is taken, and taking it is a forced load: the point is to
    // fetch the build the server is on, not to re-render the one in hand.
    [Fact]
    public async Task TakingTheOfferReloadsTheApp()
    {
        var cut = await Mounted();

        await Heard(cut, Elsewhere);

        Assert.Empty(Navigation.History);

        Assert.Single(_dialogs.Offers).Accepted();

        Assert.True(Navigation.History.Single().Options.ForceLoad);
    }

    [Fact]
    public async Task TheBuildThisClientBootedOffersNothing()
    {
        var cut = await Mounted();

        await Heard(cut, BuildVersion.Current);

        Assert.Empty(_dialogs.Offers);
    }

    [Fact]
    public async Task AServerThatSaysNothingOffersNothing()
    {
        var cut = await Mounted();

        await Heard(cut, null);

        Assert.Empty(_dialogs.Offers);
    }

    // The client's first send is the language negotiation, which runs before any component
    // exists: a deploy heard then would otherwise be announced to nobody at all.
    [Fact]
    public async Task ADeployHeardBeforeTheScreenExistsIsStillOffered()
    {
        _build.Answered(Elsewhere);

        await Mounted();

        Assert.Equal(Message, Assert.Single(_dialogs.Offers).Message);
    }

    // A tab goes on calling after a deploy, and the offer is one offer.
    [Fact]
    public async Task ASecondAnswerDoesNotStackASecondOffer()
    {
        var cut = await Mounted();

        await Heard(cut, Elsewhere);
        await Heard(cut, "and-another-one");

        Assert.Single(_dialogs.Offers);
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    async Task<IRenderedComponent<NsSetup>> Mounted()
    {
        var handoff = await Handoff.Empty();

        Services.AddSingleton(handoff.State);

        var cut = Render<NsSetup>();

        await cut.InvokeAsync(() => { });

        return cut;
    }

    // NsSetup marshals the offer onto the renderer, so the answer returns before the offer is
    // made. An empty dispatch behind it is what makes the first one already done — the
    // dispatcher is a queue, so this is a barrier and not a poll.
    async Task Heard(IRenderedComponent<NsSetup> cut, string? version)
    {
        _build.Answered(version);

        await cut.InvokeAsync(() => { });
    }
}
