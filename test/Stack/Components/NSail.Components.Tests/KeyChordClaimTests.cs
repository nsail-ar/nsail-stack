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

    static string? Badge(IRenderedComponent<IComponent> host, string css)
    {
        return host.Find($"{css} .ns-key").GetAttribute(NsKeys.Hint);
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

    // The badge is what makes the keyboard learnable from the screen, and it is drawn from the
    // claim's FIRST chord — a reminder on the way past is one key. Every worded face wears one:
    // the hero, the footer's Cancelar and a chromed button claiming a chord of its own.
    [Fact]
    public void EveryWordedFace_DrawsTheFirstChordItClaims()
    {
        var host = Mount();

        Assert.Equal(NsKeys.Submit, Badge(host, "button.hero"));
        Assert.Equal(NsKeys.Close, Badge(host, "button.cancel"));
        Assert.Equal(NsKeys.Help, Badge(host, "button.sheet"));
    }

    // THE REGRESSION (nsail#2259): the badge is decoration and a button is read by its word, in
    // two readings that are not the same. Its TEXT CONTENT, which every selector finding a
    // control by that word reads — so the chord is in an attribute and the node is empty, drawn
    // by the stylesheet with content: attr(). And its ACCESSIBLE NAME, which is what a screen
    // reader announces and what a role query asks for exactly — that one folds in generated
    // content, including a ::after on the control itself, so the badge hangs off a node of its
    // own and that node is aria-hidden. A hero worded "Guardar" answers to "Guardar", nothing
    // longer, on both.
    [Fact]
    public void TheBadge_IsInNeitherTheButtonsWordsNorItsName()
    {
        var host = Mount();

        var hero = host.Find("button.hero");
        var badge = host.Find("button.hero .ns-key");

        // The word a caller handed over, whole and alone — read on the button that carries a
        // Label, this harness composing no catalog for the hero's own key to resolve through.
        Assert.Equal("Shortcuts", host.Find("button.sheet").TextContent.Trim());
        Assert.Empty(badge.TextContent);
        Assert.Equal("true", badge.GetAttribute("aria-hidden"));

        // On the node and nowhere else: the control publishes what the layer dispatches on and
        // what a reader announces, and draws nothing.
        Assert.Null(hero.GetAttribute(NsKeys.Hint));
        Assert.Equal("Ctrl+Enter Ctrl+S", hero.GetAttribute(NsKeys.Attribute));
    }

    // A narrow surface gives the hint up before the control's own word, and a touch screen
    // never draws it at all. Read off the shipped stylesheet the way CollapsedLabelWhisperTests
    // reads the whisper's own gates: bUnit runs no container query and no media query.
    [Fact]
    public void TheBadge_IsGatedOnRoomAndOnThereBeingAKeyboard()
    {
        var css = ReadStylesheet().Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains($".ns-key::after {{\n    content: attr({NsKeys.Hint});", css, StringComparison.Ordinal);
        Assert.Contains(".ns-key {\n    display: none;", css, StringComparison.Ordinal);

        // The floor, and it has to come LAST: all three rules carry the same weight, so what
        // decides between them is the order in the file and nothing else. Said about these three
        // rules and not about the first query of each kind — the file holds several of both.
        var coarse = "@media (pointer: coarse) {\n    .ns-key {\n        display: none;";
        var room = "@container (min-width: 600px) {\n    .ns-key {\n        display: inline-block;";

        Assert.Contains(room, css, StringComparison.Ordinal);
        Assert.Contains(coarse, css, StringComparison.Ordinal);
        Assert.True(css.IndexOf(coarse, StringComparison.Ordinal) > css.IndexOf(room, StringComparison.Ordinal));
    }

    // Both shapes with no word claim the chord like any other control — the layer and a screen
    // reader read that — and draw no badge: a glyph has no word for a hint to stand beside, and
    // its name is already a hover bubble (THE WHISPER, which is the ::after they do use).
    [Theory]
    [InlineData("button.exit")]
    [InlineData("button.square")]
    public void AGlyphOnlyControl_ClaimsTheChordAndDrawsNoBadge(string css)
    {
        var host = Mount();

        var glyph = host.Find(css);

        Assert.Equal(NsKeys.Close, glyph.GetAttribute(NsKeys.Attribute));
        Assert.Empty(glyph.QuerySelectorAll(".ns-key"));
    }

    // A filter's submit claims nothing, so it draws nothing: the badge is the claim's own
    // reminder and there is no second place that decides whether to paint one.
    [Fact]
    public void AFiltersSubmit_DrawsNoBadge()
    {
        var host = Mount();

        Assert.Empty(host.Find("button.filter").QuerySelectorAll(".ns-key"));
    }

    static string ReadStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("ns-mud.css not found above the test assembly.");
    }
}
