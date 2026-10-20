// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;

namespace NSail.Components.Tests;

/// <summary>A button collapsed to its icon says its name on hover.
/// THE WHISPER is that bubble, and since nsail#2540 it is the house's only one: one element in
/// the browser's top layer, placed by ns.js from the trigger's rectangle after a steady dwell.
/// Nothing is drawn inside the control it names, which is what makes "never clipped, never
/// moves a scroll, never under the panel beside it" true of the mechanism rather than of each
/// caller.
/// <para>What a render can prove here is the DECLARATION: data-ns-whisper, the bubble's whole
/// text, and the aria-label a control carries because a container rule may take its visible word
/// away. What it cannot prove is that the bubble opens — that is a question about the RENDERED
/// control (is this word already on screen?) and about time (has the pointer been still for
/// 2.5 s?), so it is read off the shipped ns.js and ns-mud.css below, the way
/// FooterButtonCollapseTests reads the emphasis ladder. Whether the browser paints it where it
/// should is the product's own browser suite.</para></summary>
public sealed class CollapsedLabelWhisperTests : BunitContext, IAsyncLifetime
{
    public CollapsedLabelWhisperTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        // NsLink resolves its address through the root surface even with no cascade, so the
        // link half of this file needs the same registrations NsLinkDomProbeTests makes.
        Services.AddSingleton(new RouteTable(typeof(CollapsedLabelWhisperTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ACollapsingButtonCarriesTheHookAndBothStringsTheWhisperReads()
    {
        var cut = Render<NsButton>(p => p
            .Add(x => x.Label, "Confirmar")
            .Add(x => x.Icon, NsIcons.Check));

        var button = cut.Find("button");

        // The collapse: ns-collapse-sm is what the max-width container query selects, and it is
        // emitted by the same NsResponsive call that put d-c-none on the label span.
        Assert.Contains("ns-collapse-sm", button.ClassList);
        Assert.Contains("d-c-none", button.QuerySelector(".ns-button-label")!.ClassList);

        // The bubble's text, and the accessible name that survives the span going display:none.
        // Both written from Label, so neither can drift from the word it replaces.
        Assert.Equal("Confirmar", button.GetAttribute("data-ns-whisper"));
        Assert.Equal("Confirmar", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void AChromedLinkCarriesTheSamePair()
    {
        // NsPageLink derives Href and Label and renders this same chrome, so it inherits the
        // whisper with nothing of its own.
        var cut = Render<NsLink>(p => p
            .Add(x => x.Href, "/appointments")
            .Add(x => x.As, NsAs.Default)
            .Add(x => x.Label, "Reprogramar")
            .Add(x => x.Icon, NsIcons.Schedule));

        var link = cut.Find("a");

        Assert.Contains("ns-collapse-sm", link.ClassList);
        Assert.Equal("Reprogramar", link.GetAttribute("data-ns-whisper"));
        Assert.Equal("Reprogramar", link.GetAttribute("aria-label"));
    }

    /// <summary>THE DOUBLE CARTEL IS NOT A MARKUP QUESTION any more, and this is where that
    /// shows: a button the caller pinned with Breakpoint="Always" declares the same text as the
    /// one above, because whether a bubble repeating a visible word opens is answered where the
    /// word either is or is not on screen — ns.js, off the rendered control. The markup's only
    /// job is to say what there WOULD be to say, so there is no gate here to drift from a
    /// container query.</summary>
    [Fact]
    public void APinnedButtonDeclaresTheSameText_andCarriesNoCollapseHook()
    {
        var cut = Render<NsButton>(p => p
            .Add(x => x.Label, "Guardar")
            .Add(x => x.Icon, NsIcons.Save)
            .Add(x => x.Breakpoint, NsBreakpoint.Always));

        var button = cut.Find("button");

        Assert.DoesNotContain(button.ClassList, css => css.StartsWith("ns-collapse-"));
        Assert.DoesNotContain("d-c-none", button.QuerySelector(".ns-button-label")!.ClassList);
        Assert.Equal("Guardar", button.GetAttribute("data-ns-whisper"));
    }

    [Fact]
    public void AButtonWithNoLabelDeclaresNothingToSay()
    {
        // No name was handed over, so there is neither a bubble's text nor an accessible name to
        // write: the whisper has nothing to open for, and the absent attribute is how it knows.
        var cut = Render<NsButton>(p => p.Add(x => x.Icon, NsIcons.Check));

        var button = cut.Find("button");

        Assert.False(button.HasAttribute("data-ns-whisper"));
        Assert.False(button.HasAttribute("aria-label"));
    }

    /// <summary>The wordless flat rung — a grid row's actions, a title bar's utilities, the
    /// header X — on both faces, which must not part company at any width (IconOnlyStateTests).
    /// Its Label is its accessible name AND the bubble's text: handed none, each renders the
    /// vendor's bare glyph with neither, which is what the patient portal's download links were
    /// (nsail#2010).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheWordlessRungCarriesItsLabelAsItsNameAndAsTheBubblesText(bool navigates)
    {
        var cut = Render<WhisperHost>(p => p.Add(x => x.Navigates, navigates));

        var glyph = cut.Find(".mud-icon-button");

        Assert.Equal("Editar", glyph.GetAttribute("aria-label"));
        Assert.Equal("Editar", glyph.GetAttribute("data-ns-whisper"));
    }

    /// <summary>The one caller whose bubble is not a name: a chip's note is a longer form of what
    /// the chip says, and it rides the chip's OWN element rather than a wrapper — which is only
    /// true if the vendor splats an unmatched attribute onto its root, the whole reason this is
    /// read rather than assumed. A chip handed no note declares nothing.</summary>
    [Theory]
    [InlineData("Vencido el 3/4", "Vencido el 3/4")]
    [InlineData(null, null)]
    public void AChipDeclaresItsNoteOnTheChipItself(string? tooltip, string? expected)
    {
        var cut = Render<NsChip>(p => p
            .Add(x => x.Text, "Vencido")
            .Add(x => x.Tooltip, tooltip));

        Assert.Equal(expected, cut.Find(".mud-chip").GetAttribute("data-ns-whisper"));
    }

    /// <summary>A REFUSED CONTROL STILL SAYS ITS NAME — the grey pencil of a row that may not be
    /// edited, the X under a frozen form — and that is the face with nothing BUT the bubble to say
    /// what it is. The vendor drops hit testing on a disabled control
    /// (<c>.mud-button-root:disabled</c>, <c>.mud-chip.mud-disabled</c>), so the pointer never
    /// meets it and the trigger lookup walks up past it; the stylesheet hands that back to
    /// whatever declares a bubble and the attribute keeps the refusal — a disabled control takes
    /// hover and no press, measured in Chromium. What a render proves is the declaration, on both
    /// shapes a refused face has: a glyph with no word of its own, and a chip's note.</summary>
    [Fact]
    public void ARefusedControlStillDeclaresWhatTheBubbleWouldSay()
    {
        var glyph = Render<NsButton>(p => p
            .Add(x => x.Label, "Anular")
            .Add(x => x.Icon, NsIcons.Close)
            .Add(x => x.As, NsAs.Inline)
            .Add(x => x.Breakpoint, NsBreakpoint.Never)
            .Add(x => x.Disabled, true))
            .Find("button");

        Assert.True(glyph.HasAttribute("disabled"));
        Assert.Equal("Anular", glyph.GetAttribute("data-ns-whisper"));

        var chip = Render<NsChip>(p => p
            .Add(x => x.Text, "Convenio")
            .Add(x => x.Tooltip, "Cobertura vencida")
            .Add(x => x.Disabled, true))
            .Find(".mud-chip");

        Assert.Contains("mud-disabled", chip.ClassList);
        Assert.Equal("Cobertura vencida", chip.GetAttribute("data-ns-whisper"));
    }

    /// <summary>The stylesheet's half of that, which no render can show: the one rule that undoes
    /// the concession, keyed on the bubble's own declaration and on nothing about which component
    /// drew the control.</summary>
    [Fact]
    public void TheStylesheetGivesARefusedTriggerItsHitTestingBack()
    {
        var css = ReadStylesheet();

        Assert.Contains("[data-ns-whisper]:disabled,", css);
        Assert.Contains("[data-ns-whisper].mud-disabled {\n    pointer-events: auto;", css.ReplaceLineEndings("\n"));
    }

    /// <summary>And the press stays unanswered: the vendor's ripple is a document-level
    /// pointerdown over whatever carries <c>mud-ripple</c>, so it is the one response to a press
    /// that outlives both the attribute the browser refuses the press by and the CSS the rule
    /// above undid. A refused face declares no ripple and a live one does, which is what makes
    /// this the refusal's answer rather than a look.</summary>
    [Theory]
    [InlineData(NsAs.Default)]
    [InlineData(NsAs.Inline)]
    public void ARefusedFaceHasNoRippleAndALiveOneDoes(NsAs intention)
    {
        var refused = Render<NsButton>(p => p
            .Add(x => x.Label, "Facturar")
            .Add(x => x.Icon, NsIcons.Check)
            .Add(x => x.As, intention)
            .Add(x => x.Breakpoint, NsBreakpoint.Never)
            .Add(x => x.Disabled, true))
            .Find("button");

        Assert.DoesNotContain("mud-ripple", refused.ClassList);

        var live = Render<NsButton>(p => p
            .Add(x => x.Label, "Facturar")
            .Add(x => x.Icon, NsIcons.Check)
            .Add(x => x.As, intention)
            .Add(x => x.Breakpoint, NsBreakpoint.Never))
            .Find("button");

        Assert.Contains("mud-ripple", live.ClassList);
    }

    /// <summary>ONE MECHANISM, and this is the only reader that can say so: the vendor's tooltip
    /// is gone from the shared pieces altogether. Two tooltip paths is how the house came to have
    /// one bubble that flipped, closed on a press and opened at once, and another that did none
    /// of the three (nsail#2540) — so a MudTooltip reappearing anywhere in the Mud components is
    /// a second set of answers, not a convenience.</summary>
    [Fact]
    public void NoVendorTooltipSurvivesInTheSharedPieces()
    {
        var components = new DirectoryInfo(Path.Combine(RepoRoot(), "src/Stack/Components"));

        var offenders = components
            .EnumerateFiles("*.razor", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file.FullName).Contains("<MudTooltip", StringComparison.Ordinal))
            .Select(file => file.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>The stylesheet's whole part in the bubble: what it LOOKS like, once, and the box
    /// it is drawn in — never which control gets one and never which side it hangs on. Both of
    /// those were CSS's before nsail#2540, and cost nine gates, two ::after definitions and two
    /// flip rules kept in step by hand with ns.js's own selector list.</summary>
    [Fact]
    public void TheStylesheetDrawsOneBubbleAndDecidesNothingAboutIt()
    {
        var css = ReadStylesheet();

        Assert.Contains(".ns-whisper {", css);
        Assert.Contains("z-index: var(--mud-zindex-tooltip);", css);

        // Fixed, so it is in no scroll box to grow and nothing clips it where the top layer is
        // unavailable; placed from ns.js, so the stylesheet states no coordinates at all.
        Assert.Contains("position: fixed;", css);
        Assert.DoesNotContain("top: calc(100%", css);

        // Nothing is generated ON a control to name it, which is what was clipped, what grew the
        // scroll, and what joined an anchor's own accessible name (nsail#2259).
        Assert.DoesNotContain("content: attr(aria-label);", css);
        Assert.DoesNotContain("content: attr(data-ns-whisper);", css);
        Assert.DoesNotContain("data-ns-whisper-above", css);
        Assert.DoesNotContain("--ns-whisper:", css);

        // And the concession the old bubble charged every collapsed control is refunded: the
        // ripple was turned off and the box stopped clipping only so generated content could
        // escape it. There is nothing to escape now.
        Assert.DoesNotContain(".mud-ripple-effect", css);
    }

    /// <summary>ns.js owns when, where and whether — one delay for every control in the house
    /// (the story's "one value in one place"), one derived answer to the double cartel, and every
    /// exit the bubble has. Read as text because the alternative is a browser, and a browser
    /// cannot tell a constant that moved from a constant that was renamed.</summary>
    [Fact]
    public void TheScriptCarriesTheOneDelay_theDerivedGate_andEveryExit()
    {
        var script = ReadScript();

        Assert.Contains("const WHISPER_DELAY = 2500;", script);

        // The dwell is the only timing there is: the bubble is armed on two gestures and opened
        // by one timer, so there is no second number anywhere to disagree with it.
        Assert.Equal(2, Occurrences(script, "WHISPER_DELAY"));
        Assert.Contains("setTimeout(() => openWhisper(el), WHISPER_DELAY)", script);

        // Derived, not gated: the control's own rendered text is the whole of the question.
        Assert.Contains("el.innerText", script);

        // Leave, press, key, scroll and resize — and the keyboard takes the same dwell as the
        // pointer, :focus-visible being what tells its focus from the one a press leaves behind.
        foreach (var exit in new[] { "pointerleave", "pointerdown", "click", "keydown", "scroll", "resize" })
        {
            Assert.Contains(exit, script);
        }

        Assert.Contains("closeWhisper", script);

        // Asked of the element that TOOK the focus and not of the trigger, which is what answers
        // for a trigger that CONTAINS its focusable control (NsTooltip's host span, a chip whose
        // close button is what Tab reaches): a wrapper is never itself :focus-visible.
        Assert.Contains("keyboard && !target.matches(\":focus-visible\")", script);

        // The top layer, and the manual flavour of it: an auto popover would take an open menu
        // down with it, by the browser's own light-dismiss stack.
        Assert.Contains("popover = \"manual\"", script);
        Assert.Contains("showPopover()", script);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // AddMudServices registers MudBlazor's key interceptor beside the rest, and that one only
    // implements IAsyncDisposable — bUnit's synchronous teardown throws on it by design
    // (IconOnlyStateTests carries the same pair).
    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    static int Occurrences(string text, string value)
    {
        return text.Split(value).Length - 1;
    }

    static string ReadStylesheet()
    {
        return File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
    }

    static string ReadScript()
    {
        return File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/BaseServices/NSail.BaseServices.WebApp/wwwroot/ns.js"));
    }

    static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
