// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
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

/// <summary>The three stages the theme used to travel through before landing — grey, then the
/// framework default, then the organization's own — and what now makes each impossible. The
/// prerender hands its resolved brand over, so the client's first render is already the final
/// one; a provider that cannot yet tell which organization it is answering for says null
/// instead of a default Brand, so nothing repaints over it; Neutral is what is left when there
/// was no prerender to hand anything over.</summary>
public sealed class NsSetupBrandTests : BunitContext, IAsyncLifetime
{
    const string BrandKey = "NSail.Brand";
    const string ThemeKey = "NSail.Theme";

    // MudThemeProvider emits the active palette as CSS variables, so a colour is asserted in
    // the form it reaches the browser in: #1457c8 as rgba(20,87,200,1).
    const string Organization = "--mud-palette-primary: rgba(20,87,200,1);";
    const string Switched = "--mud-palette-primary: rgba(10,125,85,1);";
    const string FrameworkDefault = "--mud-palette-primary: rgba(246,142,30,1);";
    const string NeutralDark = "--mud-palette-primary: rgba(138,135,130,1);";
    const string NeutralLight = "--mud-palette-primary: rgba(117,114,109,1);";

    readonly CapturingMediator _mediator = new();

    public NsSetupBrandTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<Mediator>(_mediator);
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton<IThemeProvider>(new NullThemeProvider());
        Services.AddSingleton(new LanguageProvider { Current = "en" });

        // The chrome above every page also carries the offer to reload after a deploy
        // (ServerMovedOfferTests): nothing here moves the server's build, so nothing is
        // offered, but the seam has to be composed for the theme to paint at all.
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddSingleton<ServerBuild>();
        Services.AddScoped<PushFeed>();

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider, which NsSetup mounts, resolves a MudBlazor service that only
    // implements IAsyncDisposable and is internal to that assembly, so it cannot be replaced
    // from here the way IKeyInterceptorService is. Tearing down through xunit's IAsyncLifetime
    // makes it call BunitContext's async DisposeAsync instead of the synchronous Dispose that
    // fails on it.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public async Task PersistedBrandPaintsTheFirstRender()
    {
        var handoff = await Handoff.Carrying(BrandKey, Branded());

        // Never released: the round-trip the client would make is still in flight, which is
        // exactly the moment the flicker used to be visible.
        Use(handoff, new GatedBrandProvider());

        var cut = Render<NsSetup>();

        Assert.Equal(1, cut.RenderCount);
        Assert.Contains(Organization, cut.Markup);
        Assert.DoesNotContain(NeutralDark, cut.Markup);
        Assert.DoesNotContain(NeutralLight, cut.Markup);
        Assert.DoesNotContain(FrameworkDefault, cut.Markup);
    }

    [Fact]
    public async Task WithoutPersistedStateTheFirstRenderIsNeutral()
    {
        var handoff = await Handoff.Empty();

        Use(handoff, new GatedBrandProvider());

        var cut = Render<NsSetup>();

        Assert.Contains(NeutralDark, cut.Markup);
        Assert.DoesNotContain(Organization, cut.Markup);
        Assert.DoesNotContain(FrameworkDefault, cut.Markup);
    }

    [Fact]
    public async Task AProviderThatCannotTellYetDoesNotRepaintThePersistedBrand()
    {
        var handoff = await Handoff.Carrying(BrandKey, Branded());

        var brands = GatedBrandProvider.Open(null);

        Use(handoff, brands);

        var cut = Render<NsSetup>();

        Assert.True(brands.Asked > 0);
        Assert.Contains(Organization, cut.Markup);
        Assert.DoesNotContain(FrameworkDefault, cut.Markup);
        Assert.DoesNotContain(NeutralDark, cut.Markup);
    }

    // The other half of the same rule: null is the only way to say "not known yet", so a
    // provider that genuinely means the framework default still gets it painted. This is also
    // what proves the assertion above would have caught the default had it been applied.
    [Fact]
    public async Task ADefaultBrandTheProviderMeansIsStillPainted()
    {
        var handoff = await Handoff.Empty();

        Use(handoff, GatedBrandProvider.Open(new Brand()));

        var cut = Render<NsSetup>();

        Assert.Contains(FrameworkDefault, cut.Markup);
        Assert.DoesNotContain(NeutralDark, cut.Markup);
    }

    [Fact]
    public async Task BrandChangedReAppliesFromTheProvider()
    {
        var handoff = await Handoff.Carrying(BrandKey, Branded());

        var brands = GatedBrandProvider.Open(null);

        Use(handoff, brands);

        var cut = Render<NsSetup>();

        Assert.Contains(Organization, cut.Markup);

        brands.Answer = Branded("#0a7d55");

        await _mediator.Publish(new BrandChanged());

        cut.WaitForAssertion(() => Assert.Contains(Switched, cut.Markup));

        Assert.DoesNotContain(Organization, cut.Markup);
    }

    // nsail#484's first criterion, on the wire that already carried it: a brand saved on the
    // settings screen moves the app bar and the drawer where the reader is standing, with no
    // sign-out. The save derives the chrome, so the announce that repainted the accents
    // repaints the frame too — this is what would catch the frame being left behind.
    [Fact]
    public async Task BrandChangedMovesTheAppBarAndTheDrawerWithoutASignOut()
    {
        var handoff = await Handoff.Carrying(BrandKey, Branded());

        var brands = GatedBrandProvider.Open(null);

        Use(handoff, brands);

        var cut = Render<NsSetup>();

        var appbar = Variable(cut.Markup, "appbar-background");

        Assert.Equal(appbar, Variable(cut.Markup, "drawer-background"));

        brands.Answer = Branded("#0a7d55", "#213f36");

        await _mediator.Publish(new BrandChanged());

        cut.WaitForAssertion(() => Assert.NotEqual(appbar, Variable(cut.Markup, "appbar-background")));

        Assert.Equal(
            Variable(cut.Markup, "appbar-background"),
            Variable(cut.Markup, "drawer-background"));
    }

    static string Variable(string markup, string name)
    {
        var match = Regex.Match(markup, $"--mud-palette-{name}: ([^;]+);");

        Assert.True(match.Success, name);

        return match.Groups[1].Value;
    }

    // The server half. What the prerender persists has to be what the client above restores,
    // or the two ends drift apart silently and the flash comes back.
    [Fact]
    public async Task ThePrerenderPersistsTheBrandAndTheThemeItPainted()
    {
        var handoff = await Handoff.Empty();

        Services.AddSingleton(handoff.State);
        Services.AddSingleton<IBrandProvider>(GatedBrandProvider.Open(Branded()));
        Services.AddSingleton<IThemeProvider>(new FixedThemeProvider(darkMode: true));

        Render<NsSetup>();

        await handoff.Manager.PersistStateAsync(handoff, Renderer);

        var brand = handoff.Read<Brand>(BrandKey);
        var theme = handoff.Read<ThemeSettings>(ThemeKey);

        Assert.NotNull(brand);
        Assert.Equal("Óptica Central", brand.Name);
        Assert.Equal("#1457c8", brand.Dark.Accent);
        Assert.True(theme?.DarkMode);
    }

    void Use(Handoff handoff, IBrandProvider brands)
    {
        Services.AddSingleton(handoff.State);
        Services.AddSingleton(brands);
    }

    // A brand shaped the way a save hands it back: the chrome is already the rung the Branding
    // kit derived from the surface seed, because the derivation runs at save and never at paint.
    static Brand Branded(string accent = "#1457c8", string chrome = "#3c3f52")
    {
        var brand = new Brand { Name = "Óptica Central" };

        brand.Dark.Accent = accent;
        brand.Light.Accent = accent;
        brand.Dark.Chrome = chrome;
        brand.Light.Chrome = chrome;

        return brand;
    }
}
