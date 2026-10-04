// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Builds;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Publishing;
using NSail.Metadata;

namespace NSail.Components.Tests;

// The same offer through the real dialog manager, because what nsail#1452 promises the operator
// is a bar they can press: the words and the one button are drawn above every page by the
// provider NsSetup already mounts, and pressing the button is the reload.
public sealed class ServerMovedBarTests : BunitContext, IAsyncLifetime
{
    const string Message = "A new version is available.";
    const string Reload = "Reload";

    readonly ServerBuild _build = new();

    public ServerMovedBarTests()
    {
        Services.AddMudServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
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
        Services.AddScoped<DialogManager, MudDialogManager>();
        Services.AddSingleton(_build);
        Services.AddScoped<PushFeed>();

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    // The gesture is not issued here, deliberately (testing.md, nsail#1288): the vendor's
    // snackbar re-mints its own click handler id while it transitions, so a click aimed at the
    // element this test found dies of an id the renderer already retired — a race whose cure
    // belongs to the component, and the component is MudBlazor's. What pressing it does is
    // ServerMovedOfferTests' to prove, on the callback NsSetup hands over; what is proven here
    // is that the operator is given something to press at all.
    [Fact]
    public async Task TheOfferIsDrawnAboveThePageWithItsOneButton()
    {
        var handoff = await Handoff.Empty();

        Services.AddSingleton(handoff.State);

        var cut = Render<NsSetup>();

        await cut.InvokeAsync(() => { });

        _build.Answered("a-build-this-client-never-booted");

        // The offer is marshalled onto the renderer, and the vendor's own provider redraws when
        // its queue changes: the wait is for both hops, neither of which is being guessed at.
        cut.WaitForAssertion(() => Assert.Contains(Message, cut.Markup));

        Assert.Single(
            cut.FindAll("button"),
            button => button.TextContent.Contains(Reload, StringComparison.Ordinal));

        Assert.Single(Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Empty(Navigation.History);
    }

    // Nothing is drawn while the server is on the build this client booted with, which is every
    // request of a developer's afternoon.
    [Fact]
    public async Task TheBuildThisClientBootedDrawsNothing()
    {
        var handoff = await Handoff.Empty();

        Services.AddSingleton(handoff.State);

        var cut = Render<NsSetup>();

        _build.Answered(BuildVersion.Current);

        await cut.InvokeAsync(() => { });

        Assert.DoesNotContain(Message, cut.Markup);
        Assert.Empty(Services.GetRequiredService<ISnackbar>().ShownSnackbars);
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }
}
