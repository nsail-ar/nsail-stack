// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Icons;

namespace NSail.Components.Tests;

/// <summary>Emmanuel, 2026-08-11: a button collapsed to its icon should say its name on
/// hover. The mechanism is a CSS bubble (ns-mud.css) whose text is attr(aria-label) and whose
/// gate is the SAME container query that hid the label — so it can never double-bill a label
/// that is on screen, and it can never disagree with the word it replaces.
/// <para>bUnit lays nothing out and runs no container query, so what a render can prove here
/// is only the DOM shape the rule reads: the collapse hook and the aria-label the bubble takes
/// its text from. The rest is read off the shipped stylesheet — which thresholds the gates
/// live at, and that nothing turns the bubble on outside them — the same way
/// FooterButtonCollapseTests reads the emphasis ladder. Whether the browser paints it is the
/// Architect's probe, not this file's.</para></summary>
public sealed class CollapsedLabelWhisperTests : BunitContext
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
    public void ACollapsingButtonCarriesBothHooksTheWhisperReads()
    {
        var cut = Render<NsButton>(p => p
            .Add(x => x.Label, "Confirmar")
            .Add(x => x.Icon, NsIcons.Check));

        var button = cut.Find("button");

        // The gate: ns-collapse-sm is what the max-width container query selects, and it is
        // emitted by the same NsResponsive call that put d-c-none on the label span.
        Assert.Contains("ns-collapse-sm", button.ClassList);
        Assert.Contains("d-c-none", button.QuerySelector(".ns-button-label")!.ClassList);

        // The text: the bubble's content is attr(aria-label), so this attribute is not merely
        // the accessible name any more — it is the visible word the collapsed control shows.
        // One string, written once, which is why the two cannot drift.
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
        Assert.Equal("Reprogramar", link.GetAttribute("aria-label"));
    }

    [Fact]
    public void APinnedButtonCarriesNoCollapseHook_soItNeverWhispers()
    {
        // Breakpoint="Always" is the caller pinning the label on: the label never goes, so a
        // bubble beside it would be the double cartel the design forbids. No hook, no rule.
        var cut = Render<NsButton>(p => p
            .Add(x => x.Label, "Guardar")
            .Add(x => x.Icon, NsIcons.Save)
            .Add(x => x.Breakpoint, NsBreakpoint.Always));

        var button = cut.Find("button");

        Assert.DoesNotContain(button.ClassList, css => css.StartsWith("ns-collapse-"));
        Assert.DoesNotContain("d-c-none", button.QuerySelector(".ns-button-label")!.ClassList);
    }

    [Fact]
    public void AButtonWithNoLabelCarriesNoAriaLabel_soNoEmptyBubbleIsDrawn()
    {
        // attr(aria-label) on a control with none would generate an empty padded box. The
        // stylesheet's own [aria-label]:not([aria-label=""]) guard answers for that, and this
        // is the markup half of it: no label, no attribute.
        var cut = Render<NsButton>(p => p.Add(x => x.Icon, NsIcons.Check));

        Assert.False(cut.Find("button").HasAttribute("aria-label"));
    }

    /// <summary>The half only the stylesheet can answer for: the bubble is off by default and
    /// each breakpoint's gate sits in the SAME container query that hides that breakpoint's
    /// label — the max-width side of the pair whose min-width side turns d-c-{bp}-inline back
    /// on. Two rules written from one fact, so a threshold moved on one side and not the other
    /// is a red test rather than a button that whispers over its own visible word.</summary>
    [Theory]
    [InlineData("ns-collapse-sm", "max-width: 599.98px", "d-c-sm-inline", "min-width: 600px")]
    [InlineData("ns-collapse-md", "max-width: 959.98px", "d-c-md-inline", "min-width: 960px")]
    [InlineData("ns-collapse-lg", "max-width: 1279.98px", "d-c-lg-inline", "min-width: 1280px")]
    [InlineData("ns-collapse-xl", "max-width: 1919.98px", "d-c-xl-inline", "min-width: 1920px")]
    public void EachWhisperGateSharesItsBreakpointWithTheLabelItReplaces(
        string hook,
        string gateQuery,
        string labelUtility,
        string labelQuery)
    {
        var css = ReadStylesheet();

        Assert.Equal(gateQuery, EnclosingContainerQuery(css, $".{hook}:hover,"));
        Assert.Equal(labelQuery, EnclosingContainerQuery(css, $".{labelUtility} {{"));
    }

    [Fact]
    public void TheBubbleIsOffUntilAGateTurnsItOn_andEveryCollapseGateIsAMaxWidthQuery()
    {
        var css = ReadStylesheet();

        // The one place the text comes from, and the one place the bubble is defined: off
        // outside every query. A control whose label is visible matches this rule and nothing
        // else, which is what makes the double cartel unrepresentable rather than merely
        // avoided.
        Assert.Contains("content: attr(aria-label);", css);
        Assert.Contains("[aria-label]:not([aria-label=\"\"])::after", css);
        Assert.Contains("display: var(--ns-whisper, none);", css);

        var lines = css.Split('\n').Select(line => line.TrimEnd('\r').Trim()).ToList();

        // Each gate is a hover/focus PAIR — the bubble answers a pointer and a keyboard alike —
        // so the focus line is what counts them and the hover line above it is what names them.
        var gates = Enumerable
            .Range(1, lines.Count - 1)
            .Where(i => lines[i].EndsWith(":focus-visible {", StringComparison.Ordinal))
            .Select(i => (Hover: lines[i - 1], Focus: lines[i]))
            .ToList();

        // Eight: four Breakpoint gates, the emphasis-ladder footer's own, ns-square's, and the
        // two a menu's icon face and a lookup's end adornment take for the same reason ns-square
        // does (nsail#1865).
        Assert.Equal(8, gates.Count);
        Assert.All(gates, gate => Assert.EndsWith(":hover,", gate.Hover, StringComparison.Ordinal));

        // THE DEFECT THIS FILE MISSED FOR A MONTH (nsail#1417), now unrepresentable rather than
        // merely fixed: the definition carries a guard the gates cannot — [aria-label], so an
        // unnamed control draws no empty box — and a guard is specificity, which left every gate
        // one class-unit too light to outrank the rule it was turning on. So the two no longer
        // name the same property at all: the bubble's own rule owns display, a gate owns the
        // switch, and a line naming ::after on a :hover selector is a gate that went back to
        // racing a cascade it must lose.
        Assert.DoesNotContain(
            lines,
            line => line.Contains("::after", StringComparison.Ordinal)
                && (line.Contains(":hover", StringComparison.Ordinal) || line.Contains(":focus-visible", StringComparison.Ordinal)));

        Assert.Equal(8, lines.Count(line => line == "--ns-whisper: block;"));

        // Every gate that answers a COLLAPSE sits in the query that did the collapsing. The three
        // ungated ones are the honest exception (nsail#925, nsail#1865): an icon-only control —
        // a square at Never, a menu's icon face, a lookup's end adornment — has no width at which
        // its label comes back, so gating its bubble on a container size would only be a fiction,
        // and there is nothing to double-bill.
        var ungated = new[] { "ns-square", "ns-menu", "mud-input-adornment-icon-button" };

        foreach (var gate in gates.Where(gate => !ungated.Any(name => gate.Hover.Contains(name, StringComparison.Ordinal))))
        {
            Assert.StartsWith("max-width:", EnclosingContainerQuery(css, gate.Hover));
        }

        Assert.All(
            ungated,
            name => Assert.Equal(1, gates.Count(gate => gate.Hover.Contains(name, StringComparison.Ordinal))));
    }

    /// <summary>The container query a rule is written inside, read by scanning back from the
    /// rule's own text to the nearest @container that opened before it. Enough for this file's
    /// question — which threshold a gate sits at — and it fails loudly (an empty condition, or
    /// a missing rule) rather than passing on a stylesheet that no longer carries the rule.</summary>
    static string EnclosingContainerQuery(string css, string rule)
    {
        var index = css.IndexOf(rule, StringComparison.Ordinal);

        Assert.True(index >= 0, $"ns-mud.css no longer carries the rule '{rule}'.");

        var open = css.LastIndexOf("@container (", index, StringComparison.Ordinal);

        Assert.True(open >= 0, $"'{rule}' is not written inside any container query.");

        var start = open + "@container (".Length;

        return css[start..css.IndexOf(')', start)];
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

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
