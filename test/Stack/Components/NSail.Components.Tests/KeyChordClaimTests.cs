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

    static string? AriaLabel(IRenderedComponent<IComponent> host, string css)
    {
        return host.Find(css).GetAttribute("aria-label");
    }

    static string? Whispered(IRenderedComponent<IComponent> host, string css)
    {
        return host.Find(css).GetAttribute("data-ns-whisper");
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
        Assert.Equal("F3 Slash", Announced(host, PageBox));
        Assert.Equal("Control+K", Announced(host, MapBox));
    }

    [Fact]
    public void BothFacesOfAClose_ClaimEscape()
    {
        var host = Mount();

        Assert.Equal(NsKeys.Close, Claim(host, "button.exit"));
        Assert.Equal(NsKeys.Close, Claim(host, "button.cancel"));
    }

    // The page's box and the app MAP's box are two different boxes answering two different
    // intentions — find something on this screen, go anywhere — so neither can be reached by
    // the other's key. F3 stands first because it is the key the box draws: "/" answers only
    // while nobody is typing, so it is no reminder to somebody who already is.
    [Fact]
    public void ThePagesSearchBox_ClaimsTheFindKeyAndTheSlash()
    {
        var host = Mount();

        Assert.Equal($"{NsKeys.Find} {NsKeys.Search}", Claim(host, PageBox));
    }

    [Fact]
    public void TheAppMapsBox_ClaimsTheGoAnywhereChordAlone()
    {
        var host = Mount();

        Assert.Equal(NsKeys.Palette, Claim(host, MapBox));
    }

    // The key is DRAWN inside a search box, which is the one control that paints one: it has no
    // word to hover and therefore no bubble to carry it. Not in the placeholder — a placeholder
    // is an input's name fallback, so a key written there would land in the accessible name
    // (nsail#2259's shape one layer up) — and not part of the input at all, which is what
    // aria-hidden says.
    [Theory]
    [InlineData(".ns-nav-drawer .ns-search-box", "Ctrl+K")]
    [InlineData(".ns-search-box", "F3")]
    public void ASearchBox_DrawsTheFirstKeyItClaimsBesideTheText(string box, string key)
    {
        var host = Mount();

        var mark = host.Find($"{box} .ns-search-key");

        Assert.Equal(key, mark.TextContent.Trim());
        Assert.Equal("true", mark.GetAttribute("aria-hidden"));
        Assert.DoesNotContain(key, host.Find($"{box} input").GetAttribute("placeholder") ?? string.Empty, StringComparison.Ordinal);
    }

    // The door a screen offers onto a record takes the edit key the desktop spends on it, on
    // both faces a link has — and the bubble says the word and the key, exactly as a button's
    // does. The worded anchor carries NO aria-label: its word is its whole accessible name, and
    // the bubble naming the key is drawn nowhere near it (nsail#2540) — there is nothing on this
    // element for a name computation to fold in, which is what nsail#2272 had to outrank.
    [Fact]
    public void AScreensEditDoor_ClaimsTheEditKeyAndWhispersItWithoutJoiningItsName()
    {
        var host = Mount();

        Assert.Equal(NsKeys.Edit, Claim(host, "a.door"));
        Assert.Equal("Editar (F2)", Whispered(host, "a.door"));
        Assert.Null(AriaLabel(host, "a.door"));
        Assert.Equal("Editar", host.Find("a.door").TextContent.Trim());

        // The glyph-only face claims the same key and says its word in the same bubble, which
        // is one element in the top layer and no part of any name.
        Assert.Equal(NsKeys.Edit, Claim(host, ".glyphdoor"));
        Assert.Equal("Editar (F2)", Whispered(host, ".glyphdoor"));
    }

    // A link with no chord publishes nothing and says nothing about one — there is no second
    // place deciding whether to paint a bubble, here either.
    [Fact]
    public void ALinkWithNoChord_ClaimsNothingAndCarriesNoName()
    {
        var host = Mount();

        Assert.Null(Claim(host, "a.plain"));
        Assert.Null(Announced(host, "a.plain"));
        Assert.Null(AriaLabel(host, "a.plain"));

        // The bubble's text is the plain word, and a word already on screen is a bubble that
        // never opens: whether it does is one question about the rendered control, asked where
        // the control is rendered (ns.js, whisperAdds) and nowhere here.
        Assert.Equal("Ir", Whispered(host, "a.plain"));
    }

    // The page's box and the map's, told apart by where they stand: the map's is the one inside
    // the drawer, which is also how the keyboard layer tells them apart (ns.js, chordTarget).
    const string MapBox = ".ns-nav-drawer input[data-ns-keys]";
    const string PageBox = "form input[data-ns-keys]";

    // The ruling on nsail#2207 (2026-10-14): the chord is the control's TOOLTIP, never a drawn
    // badge. The tooltip's text rides data-ns-whisper, built from the word and the claim's FIRST
    // chord — a reminder on the way past is one key. Every worded face wears one: the hero, the
    // footer's Cancelar and a chromed button claiming a chord of its own.
    [Fact]
    public void EveryWordedFace_WhispersItsWordAndTheFirstChordItClaims()
    {
        var host = Mount();

        Assert.Equal("Common.Submit (Ctrl+Enter)", Whispered(host, "button.hero"));
        Assert.Equal("Common.Close (Esc)", Whispered(host, "button.cancel"));
        Assert.Equal("Shortcuts (?)", Whispered(host, "button.sheet"));
    }

    // THE REGRESSION THE BADGE CAUSED (nsail#2259), unrepresentable rather than merely fixed: a
    // decoration drawn ON the control joins its ACCESSIBLE NAME, which folds in generated
    // content a ::after on the control itself would add, UNLESS aria-label is there to outrank
    // it — so a hero worded "Guardar" answered to "Guardar Ctrl+Enter" once. The bubble is drawn
    // on no control at all now (nsail#2540) and its text rides a SEPARATE attribute
    // (data-ns-whisper above) that no name computation reads: the name stays the word.
    [Fact]
    public void TheWhisperedChord_NeverJoinsTheButtonsNameOrItsWords()
    {
        var host = Mount();

        // The word alone, on both the button that carries a Label and the one whose word is
        // Strings' own fallback (this harness composing no catalog for either to resolve
        // through) — nothing appended by the whisper that named the same two strings above.
        Assert.Equal("Shortcuts", host.Find("button.sheet").TextContent.Trim());
        Assert.Equal("Common.Submit", host.Find("button.hero").TextContent.Trim());

        // The accessible name — aria-label, which these faces carry because a container rule
        // may take the visible word away — is the bare word on every one of them, never the
        // chord.
        Assert.Equal("Shortcuts", AriaLabel(host, "button.sheet"));
        Assert.Equal("Common.Submit", AriaLabel(host, "button.hero"));
        Assert.Equal("Common.Close", AriaLabel(host, "button.cancel"));

        // On the claim's own attribute and nowhere else: the control publishes what the layer
        // dispatches on and what a reader announces, and draws no child node for either.
        Assert.Equal("Ctrl+Enter Ctrl+S", host.Find("button.hero").GetAttribute(NsKeys.Attribute));
    }

    // Both shapes with no word claim the chord like any other control — the layer and a screen
    // reader read that — and their whisper is the one bubble in the top layer (THE WHISPER),
    // never a node of their own.
    [Theory]
    [InlineData("button.exit")]
    [InlineData("button.square")]
    public void AGlyphOnlyControl_ClaimsTheChordAndWhispersItsWordAlone(string css)
    {
        var host = Mount();

        var glyph = host.Find(css);

        Assert.Equal(NsKeys.Close, glyph.GetAttribute(NsKeys.Attribute));
        Assert.Empty(glyph.QuerySelectorAll("kbd"));
    }

    // A filter's submit claims nothing, so it whispers nothing: there is no second place that
    // decides whether to paint a bubble.
    [Fact]
    public void AFiltersSubmit_WhispersNothing()
    {
        var host = Mount();

        Assert.Null(AriaLabel(host, "button.filter"));
        Assert.Null(Whispered(host, "button.filter"));
    }

    // No control with a WORD draws its chord as a node any more (nsail#2272): a word and a badge
    // beside it were one mechanism read twice, and the stylesheet is where the badge could come
    // back by accident. The search box's own mark is the exception the rule names — a box has no
    // word to hover — and it is its own rule, not the retired one.
    [Fact]
    public void TheStylesheet_CarriesNoBadgeRuleAndRidesTheWhisperInstead()
    {
        var css = ReadStylesheet();

        Assert.DoesNotContain(".ns-key", css, StringComparison.Ordinal);
        Assert.Contains(".ns-whisper", css, StringComparison.Ordinal);
        Assert.Contains(".ns-search-key", css, StringComparison.Ordinal);
    }

    // THE FAILURE THIS SUITE WAS MISSING: a chord can be declared as a constant, published by a
    // control and announced to a screen reader while NOTHING dispatches it — which is what
    // Palette and Help were for two releases, a key the house promised and the keyboard did not
    // answer. The claim's other half is the layer's own table, so it is read here rather than
    // left to a browser suite nobody runs on a constant's rename.
    [Fact]
    public void TheKeyboardLayer_DispatchesEveryChordTheHouseDeclares()
    {
        var layer = ReadKeyboardLayer();

        foreach (var chord in new[] { NsKeys.Submit, NsKeys.Save, NsKeys.Palette, NsKeys.Search, NsKeys.Help, NsKeys.Find, NsKeys.Edit })
        {
            Assert.Contains($"chord: \"{chord}\"", layer, StringComparison.Ordinal);
        }

        // Escape is the one claim that is declared and never dispatched: the surfaces answer it
        // themselves, through the guard the X goes through, and a second dispatcher would close
        // them twice.
        Assert.DoesNotContain($"chord: \"{NsKeys.Close}\"", layer, StringComparison.Ordinal);
    }

    // WCAG 2.1.4 is about a chord somebody could be in the middle of TYPING, which is the two
    // written as characters and nothing else: a function key writes none, so holding F3 back
    // while the cursor is in a box would cost it the half of its job the desktop spends it on.
    [Fact]
    public void OnlyTheChordsThatAreCharacters_AreHeldBackWhileSomebodyIsTyping()
    {
        var layer = ReadKeyboardLayer();

        foreach (var chord in new[] { NsKeys.Search, NsKeys.Help })
        {
            Assert.Contains($"chord: \"{chord}\", key: \"{chord}\", modified: false, character: true", layer, StringComparison.Ordinal);
        }

        foreach (var chord in new[] { NsKeys.Find, NsKeys.Edit })
        {
            Assert.Contains($"chord: \"{chord}\", key: \"{chord.ToLowerInvariant()}\", modified: false, character: false", layer, StringComparison.Ordinal);
        }
    }

    static string ReadStylesheet()
    {
        return ReadSource("src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css");
    }

    static string ReadKeyboardLayer()
    {
        return ReadSource("src/Stack/BaseServices/NSail.BaseServices.WebApp/wwwroot/ns.js");
    }

    static string ReadSource(string path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, path));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"{path} not found above the test assembly.");
    }
}
