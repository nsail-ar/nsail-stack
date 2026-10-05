// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Tones;

namespace NSail.Components.Tests;

/// <summary>nsail#764: the status channel is one vocabulary with two forms — a word painted by
/// its tone where the word fits, a dot where it does not. Both read the same four severities
/// plus the neutral, and a caller never names a colour or a per-status class. The pair is what
/// makes "State paints its text" (intentional-ui.md) reachable from a screen with no room for
/// text, and the accessible name is what keeps a dot from being colour alone.</summary>
public sealed class NsStatusToneTests : BunitContext
{
    public NsStatusToneTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    public static TheoryData<NsSeverity?, string> Tones()
    {
        return new TheoryData<NsSeverity?, string>
        {
            { NsSeverity.Info, "ns-status-info" },
            { NsSeverity.Success, "ns-status-success" },
            { NsSeverity.Warning, "ns-status-warning" },
            { NsSeverity.Error, "ns-status-error" },
            { null, "ns-status-muted" },
        };
    }

    [Theory]
    [MemberData(nameof(Tones))]
    public void AWordTakesItsToneFromTheSeverity(NsSeverity? severity, string expected)
    {
        var cut = Render<NsStatusText>(p => p
            .Add(x => x.Severity, severity)
            .AddChildContent("Confirmado"));

        var text = cut.Find(".ns-status-text");

        Assert.Contains(expected, text.ClassList);
        Assert.Equal("Confirmado", text.TextContent);
    }

    [Theory]
    [MemberData(nameof(Tones))]
    public void ADotTakesTheSameToneFromTheSameSeverity(NsSeverity? severity, string expected)
    {
        var cut = Render<NsStatusDot>(p => p
            .Add(x => x.Severity, severity)
            .Add(x => x.Title, "Confirmado"));

        Assert.Contains(expected, cut.Find(".ns-status-dot").ClassList);
    }

    /// <summary>nsail#1325: the same channel on a GLYPH — the warning mark beside the sentence
    /// that says what is wrong, which is the whole of what a block of colour was doing at the
    /// top of a card. A caller still names a severity and never a colour.</summary>
    [Theory]
    [InlineData(NsSeverity.Info, "ns-status-info")]
    [InlineData(NsSeverity.Success, "ns-status-success")]
    [InlineData(NsSeverity.Warning, "ns-status-warning")]
    [InlineData(NsSeverity.Error, "ns-status-error")]
    public void AGlyphTakesTheSameToneFromTheSameSeverity(NsSeverity severity, string expected)
    {
        var cut = Render<NsIcon>(p => p
            .Add(x => x.Icon, NsIcons.Warning)
            .Add(x => x.Severity, severity));

        var glyph = cut.Find(".mud-icon-root");

        Assert.Contains("ns-status-text", glyph.ClassList);
        Assert.Contains(expected, glyph.ClassList);
    }

    /// <summary>Null is the ordinary glyph and NOT the neutral paint a dot takes: an icon that
    /// reports no state is the ink of whatever it names — a card's title, a button's word — and
    /// a tone class there would repaint every glyph in the app.</summary>
    [Fact]
    public void AGlyphWithNoSeverityNamesNoTone()
    {
        var cut = Render<NsIcon>(p => p.Add(x => x.Icon, NsIcons.Warning));

        Assert.DoesNotContain("ns-status-text", cut.Find(".mud-icon-root").ClassList);
    }

    /// <summary>The vendor paints every icon its own action ink at one class of specificity, so
    /// the channel is restated against the icon itself rather than left to win on file order —
    /// a rank nobody wrote is a rank a vendor bump moves in silence (the CardIconRankTests
    /// idiom: bUnit paints nothing, so the rule is read off the shipped stylesheet).</summary>
    [Fact]
    public void AGlyphsToneOutranksTheVendorsOwnIconInk()
    {
        Assert.Contains(
            """
            .mud-icon-root.ns-status-text {
                color: var(--ns-status-color, inherit);
            }
            """,
            ReadStylesheet());
    }

    /// <summary>The neutral is a live state at the MUTED rank, and the ink a refused control
    /// wears is the one thing it may not be: that ink is held under 3:1 on purpose
    /// (BrandThemeContrastTests), so a status word painted in it reads as a control nobody may
    /// touch. The rank is leaned a tenth into the scheme's ink for the ground a row gives it
    /// under the pointer — what the token resolves to is measured there; that it is this token is
    /// read off the stylesheet here, because bUnit paints nothing.</summary>
    [Fact]
    public void TheNeutralIsTheMutedRankAndNotTheRefusedInk()
    {
        var css = ReadStylesheet();

        Assert.Contains(
            $$"""
            .ns-status-muted {
                --ns-status-color: color-mix(in srgb, var(--mud-palette-text-secondary) {{Percent(BrandTone.NeutralLean)}}%, var(--mud-palette-text-primary));
            }
            """,
            css);

        Assert.DoesNotContain("--ns-status-color: var(--mud-palette-text-disabled)", css);
    }

    /// <summary>nsail#1935: the row wash SURVIVES THE POINTER. Mud answers a hover by replacing
    /// the row's background-color from five rules that no single class can outrank, so a washed
    /// row used to lose its tone exactly when it was being read — the condition channel blank and
    /// the word left on the vendor's neutral tint. The rule hands the vendor's own variable the
    /// row's tone deepened instead, which is the cascade and not a specificity fight (the glyph
    /// above is the one place a vendor selector is restated, and for the opposite reason: there
    /// is no variable there). What the two mixes resolve to is graded in
    /// BrandThemeContrastTests; that the row sets them both is read off the stylesheet here.</summary>
    [Theory]
    [InlineData("danger", "error")]
    [InlineData("warning", "warning")]
    [InlineData("success", "success")]
    public void AWashedRowKeepsItsToneUnderThePointer(string row, string severity)
    {
        Assert.Contains(
            $$"""
            .ns-row-{{row}} {
                background-color: color-mix(in srgb, var(--mud-palette-{{severity}}) {{Percent(BrandTone.RowWash)}}%, transparent);
                --mud-palette-table-hover: color-mix(in srgb, var(--mud-palette-{{severity}}) {{Percent(BrandTone.RowWashPointed)}}%, transparent);
            }
            """,
            ReadStylesheet());
    }

    /// <summary>nsail#1935: the other four are the severity LEANED INTO the scheme's own ink, not
    /// the severity itself — a severity constant is a fill, and a fill set as a word on a card
    /// cleared nothing in light (Warning 3.32:1, Info 3.70:1). One declaration serves both
    /// schemes because the ink it leans into flips with the scheme. What the lean resolves to is
    /// measured in BrandThemeContrastTests; that the classes carry it, and that none of them is
    /// left pointing at the bare fill, is read off the stylesheet here.</summary>
    [Theory]
    [InlineData("info")]
    [InlineData("success")]
    [InlineData("warning")]
    [InlineData("error")]
    public void ATonedStatusLeansItsSeverityIntoTheSchemesInk(string severity)
    {
        var css = ReadStylesheet();

        Assert.Contains(
            $$"""
            .ns-status-{{severity}} {
                --ns-status-color: color-mix(in srgb, var(--mud-palette-{{severity}}) {{Percent(BrandTone.ToneLean)}}%, var(--mud-palette-text-primary));
            }
            """,
            css);

        Assert.DoesNotContain($"--ns-status-color: var(--mud-palette-{severity})", css);
    }

    // Every fraction in this file comes from BrandTone and none is ever typed: the stylesheet and
    // the arithmetic that grades it have to be reading one number, or the proof drifts off the
    // paint. Rounded rather than formatted to an integer, so a weight that is not a whole
    // percent fails loudly here instead of matching a string nobody wrote.
    static string Percent(double weight)
    {
        return Math.Round(weight * 100, 4).ToString(CultureInfo.InvariantCulture);
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

    /// <summary>Five states, five readings: the four severities plus the neutral are five
    /// distinct classes, so no two states can paint the same.</summary>
    [Fact]
    public void TheFiveTonesAreFiveDistinctClasses()
    {
        var classes = Tones().Select(row => (string)row[1]!).ToList();

        Assert.Equal(5, classes.Count);
        Assert.Equal(classes.Count, classes.Distinct().Count());
    }

    [Fact]
    public void ADotCarriesItsStateInWords()
    {
        var cut = Render<NsStatusDot>(p => p
            .Add(x => x.Severity, NsSeverity.Warning)
            .Add(x => x.Title, "Ausente"));

        var dot = cut.Find(".ns-status-dot");

        Assert.Equal("img", dot.GetAttribute("role"));
        Assert.Equal("Ausente", dot.GetAttribute("aria-label"));
        Assert.Equal("Ausente", dot.GetAttribute("title"));
    }

    /// <summary>The neutral is a paint, never an absence: a dot with no severity still names a
    /// tone class, because an unresolved custom property draws a transparent circle.</summary>
    [Fact]
    public void TheNeutralDotStillNamesATone()
    {
        var cut = Render<NsStatusDot>(p => p
            .Add(x => x.Severity, (NsSeverity?)null)
            .Add(x => x.Title, "Completado"));

        var classes = cut.Find("span").ClassList;

        Assert.Contains("ns-status-dot", classes);
        Assert.Contains("ns-status-muted", classes);
    }
}
