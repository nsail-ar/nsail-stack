// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

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
    /// touch. What the token resolves to is measured there; that it is this token is read off
    /// the stylesheet here, because bUnit paints nothing.</summary>
    [Fact]
    public void TheNeutralIsTheMutedRankAndNotTheRefusedInk()
    {
        var css = ReadStylesheet();

        Assert.Contains(
            """
            .ns-status-muted {
                --ns-status-color: var(--mud-palette-text-secondary);
            }
            """,
            css);

        Assert.DoesNotContain("--ns-status-color: var(--mud-palette-text-disabled)", css);
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
