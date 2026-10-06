// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSail.Builds;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Publishing;
using NSail.Metadata;

namespace NSail.Components.Tests;

// Where a toast lands and what it takes from the person while it is there (nsail#2042). Read
// off the vendor's own markup through the house's own registration, because both halves are
// the vendor's to draw: the position is one setting on SnackbarConfiguration, and the class
// hook ns-mud.css hangs pointer-events:none on rides a per-toast SnackbarTypeClass. What the
// rule then does to a real click is the browser's to prove.
public sealed class ToastPlacementTests : BunitContext, IAsyncLifetime
{
    const string Saved = "Saved.";
    const string NewVersion = "A new version is available.";
    const string Reload = "Reload";

    public ToastPlacementTests()
    {
        Services.AddComponentServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Saved"] = Saved,
            ["Common.NewVersion"] = NewVersion,
            ["Common.Reload"] = Reload,
        })]));
        Services.AddSingleton(new LanguageProvider { Current = "en" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<ServerBuild>();
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

    async Task<IRenderedComponent<NsSetup>> RenderHost()
    {
        var handoff = await Handoff.Empty();

        Services.AddSingleton(handoff.State);

        var cut = Render<NsSetup>();

        await cut.InvokeAsync(() => { });

        return cut;
    }

    // The vendor's default is top-right, straight over the title bar's action row. Asserted on
    // the container the provider draws, which is the only element the position is written on.
    [Fact]
    public async Task TheToastHostSitsAtTheBottomCenter()
    {
        var cut = await RenderHost();

        Assert.Contains(
            "mud-snackbar-location-bottom-center",
            cut.Find("#mud-snackbar-container").ClassName);
    }

    // A success toast holds nothing to press: no close icon, no action, and the class that
    // takes it out of the hit test. Pressed for the absence of a button, not for the words.
    [Fact]
    public async Task ASuccessToastOffersNothingToPress()
    {
        var cut = await RenderHost();
        var dialogs = Services.GetRequiredService<DialogManager>();

        await cut.InvokeAsync(() => dialogs.Notify(Saved, NsSeverity.Success));

        cut.WaitForAssertion(() => Assert.Contains(Saved, cut.Markup));

        var toast = cut.Find("[role=alert].mud-snackbar");

        Assert.Contains("ns-toast", toast.ClassName);
        Assert.Empty(toast.QuerySelectorAll("button"));
    }

    // The deploy-landed offer is the other half of the ruling: it moved with the position, and
    // it kept everything a person can take hold of.
    [Fact]
    public async Task TheReloadOfferKeepsItsOneButton()
    {
        var cut = await RenderHost();
        var dialogs = Services.GetRequiredService<DialogManager>();

        await cut.InvokeAsync(() => dialogs.Offer(NewVersion, Reload, () => { }));

        cut.WaitForAssertion(() => Assert.Contains(NewVersion, cut.Markup));

        var toast = cut.Find("[role=alert].mud-snackbar");

        Assert.DoesNotContain("ns-toast", toast.ClassName);
        Assert.Single(
            toast.QuerySelectorAll("button"),
            button => button.TextContent.Contains(Reload, StringComparison.Ordinal));
    }
}
