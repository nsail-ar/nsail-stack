// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2207: the system is drivable from the keyboard, and the whole of how a chord
/// finds the act it runs is the CLAIM a control publishes on itself — data-ns-keys, which the
/// layer in ns.js dispatches on, and aria-keyshortcuts, which is the same claim where it is
/// read rather than seen.
///
/// <para>So this suite is that claim's other half, pinned the way NsComboboxExpandedMarkTests
/// pins the mark its own guard keys on: the dispatch itself cannot be proved at this layer —
/// bUnit renders through AngleSharp, which runs no script and performs no implicit form
/// submission — and is held by the browser suites. What is held here is that the controls go on
/// publishing what the layer looks for, so the day one stops the failure has a name instead of
/// a keyboard that quietly does nothing.</para></summary>
public sealed class KeyChordClaimTests : BunitContext, IAsyncLifetime
{
    public KeyChordClaimTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<ChordHost> Mount(bool untracked = false)
    {
        return Render<ChordHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(KeyChordClaimTests).Assembly, []))
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Untracked, untracked));
    }

    static string? Claim(IRenderedComponent<IComponent> host, string css)
    {
        return host.Find(css).GetAttribute(NsKeys.Attribute);
    }

    static string? Announced(IRenderedComponent<IComponent> host, string css)
    {
        return host.Find(css).GetAttribute("aria-keyshortcuts");
    }

    [Fact]
    public void AFormHoldingADocument_ClaimsBothChordsOnItsHero()
    {
        var host = Mount();

        Assert.Equal("Ctrl+Enter Ctrl+S", Claim(host, "button.hero"));
    }

    // The desktop habit means "save this document", so a form that holds none does not take the
    // chord away from the browser it belongs to.
    [Fact]
    public void AFormHoldingNoDocument_ClaimsOnlyCtrlEnter()
    {
        var host = Mount(untracked: true);

        Assert.Equal("Ctrl+Enter", Claim(host, "button.hero"));
    }

    // A filter's submit is a control over what a list shows, not the act of the form the cursor
    // is in: a screen holding both would offer Ctrl+Enter twice.
    [Fact]
    public void AFiltersSubmit_ClaimsNothing()
    {
        var host = Mount();

        Assert.Null(Claim(host, "button.filter"));
        Assert.Null(Announced(host, "button.filter"));
    }

    // Spelled in the grammar aria-keyshortcuts is read with, derived from the same string the
    // badge draws — so the key announced, the key drawn and the key dispatched cannot drift
    // into being three different keys.
    [Fact]
    public void EveryClaim_IsAnnouncedInThePlatformsOwnSpelling()
    {
        var host = Mount();

        Assert.Equal("Control+Enter Control+S", Announced(host, "button.hero"));
        Assert.Equal("Escape", Announced(host, "button.cancel"));
        Assert.Equal("Slash", Announced(host, "input[data-ns-keys]"));
    }

    [Fact]
    public void BothFacesOfAClose_ClaimEscape()
    {
        var host = Mount();

        Assert.Equal(NsKeys.Close, Claim(host, "button.exit"));
        Assert.Equal(NsKeys.Close, Claim(host, "button.cancel"));
    }

    [Fact]
    public void ThePagesSearchBox_ClaimsTheSlash()
    {
        var host = Mount();

        Assert.Equal(NsKeys.Search, Claim(host, "input[data-ns-keys]"));
    }

    // The badge is what makes the keyboard learnable from the screen, and it rides the control
    // that answers the chord. Hidden from a screen reader because the claim above already says
    // the same thing where that reader looks for it.
    [Fact]
    public void TheHero_DrawsItsChordOnItself()
    {
        var host = Mount();

        var badge = host.Find("button.hero kbd.ns-key");

        Assert.Equal(NsKeys.Submit, badge.TextContent.Trim());
        Assert.Equal("true", badge.GetAttribute("aria-hidden"));
    }

    // A narrow surface gives the hint up before the control's own word: the badge rides the
    // same container query the label does (NsResponsive.ShownFromContainer).
    [Fact]
    public void TheBadge_IsGoneUntilItsContainerHasRoom()
    {
        var host = Mount();

        var classes = host.Find("button.hero kbd.ns-key").GetAttribute("class");

        Assert.Contains("d-c-none", classes, StringComparison.Ordinal);
        Assert.Contains("d-c-sm-inline", classes, StringComparison.Ordinal);
    }

    // The X has no word for a badge to stand next to — its own name is a hover bubble — so it
    // claims the chord and draws nothing, while the worded close beside it draws Esc.
    [Fact]
    public void AGlyphOnlyClose_ClaimsTheChordAndDrawsNoBadge()
    {
        var host = Mount();

        Assert.Empty(host.FindAll("button.exit kbd.ns-key"));
        Assert.Equal(NsKeys.Close, host.Find("button.cancel kbd.ns-key").TextContent.Trim());
    }
}
