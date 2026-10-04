// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Icons;

namespace NSail.Components.Tests;

/// <summary>One button, intentions only (nsail#925). <c>NsAs</c> says what the act MEANS and
/// nothing else, and <b>whether the text is drawn is <c>NsBreakpoint</c>'s word alone</b>:
/// <c>Always</c> pins it on, <c>Xs</c>…<c>Xxl</c> hand it over from a container width up, and
/// <c>Never</c> leaves the glyph with the label as its name. Icon-only is that last state — a
/// state of the label, never a rank — so every intention has one, and the intention still
/// decides what it LOOKS like: the flat rung keeps the vendor's bare glyph, every intention
/// with a fill takes a square. <c>NsButton</c>, <c>NsLink</c> and <c>NsPageLink</c> answer
/// identically, which is the whole point of the fork this story closed.
/// <para>bUnit lays nothing out and paints nothing, so a render proves the classes the rule
/// selects by and the stylesheet is read for the geometry itself — the CollapsedLabelWhisperTests
/// idiom.</para></summary>
public sealed class IconOnlyStateTests : BunitContext, IAsyncLifetime
{
    public IconOnlyStateTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(new RouteTable(typeof(IconOnlyStateTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // The flat rung's glyph pairs itself with a MudTooltip, which pulls in the vendor's popover
    // service — and that one is IAsyncDisposable only, so the synchronous teardown throws.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    /// <summary>The whole vocabulary, read by reflection rather than left to the compiler: a
    /// value re-added under one of the old names would compile everywhere it is written and
    /// quietly restore the fork this story closed. No value names a look, a size or a
    /// state.</summary>
    [Fact]
    public void NsAsNamesIntentionsAndNothingElse()
    {
        Assert.Equal(
            ["Default", "Main", "Important", "Inline", "Submit", "Filter", "Send", "Danger"],
            Enum.GetNames<NsAs>());
    }

    /// <summary>A button with no As is the grey filled button — the enum's own default carries
    /// that, so no component has to restate it and no page has to write it.</summary>
    [Fact]
    public void DefaultIsWhatAControlThatSaidNothingGets()
    {
        Assert.Equal(NsAs.Default, default);

        var button = Render<NsButton>(p => p.Add(x => x.Label, "Guardar")).Find("button");

        Assert.Contains("mud-button-filled", button.ClassList);
        Assert.DoesNotContain("mud-button-filled-primary", button.ClassList);
    }

    /// <summary>Text visibility lives in the breakpoint and nowhere else (Leonardo, nsail#925:
    /// "es la opción que menos miente; IconOnly es otro flag más que termina afectando
    /// breakpoint"). Asked of the parameter list rather than of a render, because a second knob
    /// is a compile-time fact no markup can show.</summary>
    [Theory]
    [InlineData(typeof(NsButton))]
    [InlineData(typeof(NsLink))]
    [InlineData(typeof(NsPageLink<>))]
    [InlineData(typeof(NsAction))]
    public void NoControlCarriesASecondKnobForTheLabel(Type control)
    {
        Assert.DoesNotContain(Parameters(control), property => property.Name == "IconOnly");
        Assert.Contains(Parameters(control), property => property.PropertyType == typeof(NsBreakpoint));
    }

    /// <summary>NsBreakpoint is its own vocabulary and NsSize is untouched: the two ends of a
    /// breakpoint are not sizes, and everything else in the app still measures in NsSize.</summary>
    [Fact]
    public void TheBreakpointIsItsOwnEnumAndNsSizeIsUnmoved()
    {
        Assert.Equal(
            ["Always", "Xs", "Sm", "Md", "Lg", "Xl", "Xxl", "Never"],
            Enum.GetNames<NsBreakpoint>());

        Assert.Equal(["None", "Xs", "Sm", "Md", "Lg", "Xl", "Xxl"], Enum.GetNames<NsSize>());
    }

    /// <summary>The flat rung has no box to square off, so its wordless face is the vendor's
    /// own icon button — pixel-identical to what a grid row's actions, a title bar's utilities
    /// and the header X have always worn. A square there would repaint every toolbar in the
    /// app.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheFlatRankWithNoWord_IsTheBareGlyph(bool link)
    {
        var button = link
            ? RenderLink(NsAs.Inline, NsBreakpoint.Never).Find("a")
            : Render<NsButton>(p => p
                .Add(x => x.As, NsAs.Inline)
                .Add(x => x.Breakpoint, NsBreakpoint.Never)
                .Add(x => x.Icon, NsIcons.Edit)
                .Add(x => x.Label, "Editar")).Find("button");

        Assert.Contains("mud-icon-button", button.ClassList);
        Assert.DoesNotContain("ns-square", button.ClassList);
        Assert.DoesNotContain("mud-button-filled", button.ClassList);
    }

    /// <summary>Every intention with a fill wears the same square in the same state — the fill
    /// and the shadow are the intention's, the shape is the house's. Important is read here
    /// beside Default because the two must differ in nothing but the wash.</summary>
    [Theory]
    [InlineData(NsAs.Default, false)]
    [InlineData(NsAs.Important, false)]
    [InlineData(NsAs.Main, false)]
    [InlineData(NsAs.Default, true)]
    [InlineData(NsAs.Important, true)]
    [InlineData(NsAs.Main, true)]
    public void AFilledRankAtNever_IsASquareAndKeepsItsFill(NsAs rank, bool link)
    {
        var control = link
            ? RenderLink(rank, NsBreakpoint.Never).Find("a")
            : Render<NsButton>(p => p
                .Add(x => x.As, rank)
                .Add(x => x.Breakpoint, NsBreakpoint.Never)
                .Add(x => x.Icon, NsIcons.Edit)
                .Add(x => x.Label, "Editar")).Find("button");

        Assert.Contains("ns-square", control.ClassList);
        Assert.Contains("mud-button-filled", control.ClassList);
        Assert.DoesNotContain("mud-icon-button", control.ClassList);

        // The intention survives the state: a wordless Important is still Important, and a
        // wordless Main still spends the screen's one full-strength accent.
        Assert.Equal(rank == NsAs.Important, control.ClassList.Contains("ns-important"));
        Assert.Equal(rank == NsAs.Main, control.ClassList.Contains("mud-button-filled-primary"));
    }

    /// <summary>The Label is the NAME at Never, never the text: it reaches the accessible name
    /// and the whisper, and the span the collapse rule hooks stays empty. A drawn label would be
    /// the rectangle the square exists to end.</summary>
    [Fact]
    public void AtNever_TheLabelIsTheNameAndNotTheText()
    {
        var cut = Render<NsButton>(p => p
            .Add(x => x.Breakpoint, NsBreakpoint.Never)
            .Add(x => x.Icon, NsIcons.Edit)
            .Add(x => x.Label, "Editar"));

        var button = cut.Find("button");

        Assert.Equal("Editar", button.GetAttribute("aria-label"));
        Assert.Empty(button.QuerySelector(".ns-button-label")!.TextContent);
    }

    /// <summary>The other way into the state, and it needs no parameter: a control handed an
    /// Icon and no Label has no word to draw, so it is icon-only by construction rather than by
    /// declaration.</summary>
    [Fact]
    public void AControlWithNoLabelAtAll_IsIconOnlyWithoutSayingSo()
    {
        var cut = Render<NsButton>(p => p.Add(x => x.Icon, NsIcons.Check));

        Assert.Contains("ns-square", cut.Find("button").ClassList);
    }

    /// <summary>Never collapses unconditionally: no container query is written at all, because
    /// there is no width at which the label comes back. The whisper would otherwise be gated
    /// twice — once ungated by ns-square and once by the query — and draw two bubbles on one
    /// hover.</summary>
    [Fact]
    public void AtNever_TheCollapseIsUnconditionalAndCarriesNoContainerQuery()
    {
        var cut = Render<NsButton>(p => p
            .Add(x => x.Breakpoint, NsBreakpoint.Never)
            .Add(x => x.Icon, NsIcons.Edit)
            .Add(x => x.Label, "Editar"));

        var button = cut.Find("button");

        Assert.Contains("ns-square", button.ClassList);
        Assert.DoesNotContain(button.ClassList, css => css.StartsWith("ns-collapse-"));
        Assert.DoesNotContain(button.QuerySelector(".ns-button-label")!.ClassList, css => css.StartsWith("d-c-"));
    }

    /// <summary>A width in the middle still swaps text for glyph at its own boundary, and that
    /// is the pair a render can see: the label span hidden by default and handed back from the
    /// breakpoint up, and the control marked so the stylesheet can square it for as long as the
    /// query holds. Read on the link too — a command and a link at one width must not part
    /// company.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AtSm_TheTextIsSwappedForTheGlyphAtTheBoundary(bool link)
    {
        var control = link
            ? RenderLink(NsAs.Default, NsBreakpoint.Sm).Find("a")
            : Render<NsButton>(p => p
                .Add(x => x.Breakpoint, NsBreakpoint.Sm)
                .Add(x => x.Icon, NsIcons.Edit)
                .Add(x => x.Label, "Editar")).Find("button");

        Assert.Contains("ns-collapse-sm", control.ClassList);
        Assert.DoesNotContain("ns-square", control.ClassList);

        var label = control.QuerySelector(".ns-button-label")!;

        Assert.Contains("d-c-none", label.ClassList);
        Assert.Contains("d-c-sm-inline", label.ClassList);
        Assert.Equal("Editar", label.TextContent);
    }

    /// <summary>The boundary itself, which no render can see: the sm container query is where
    /// the swapped label leaves the same square behind that Never wears outright — one shape
    /// for one state, however the state was reached.</summary>
    [Fact]
    public void TheSmBoundaryIsWhereTheSquareTakesOver()
    {
        var css = ReadStylesheet();
        var rule = css.IndexOf(".ns-collapse-sm.mud-button {", StringComparison.Ordinal);

        Assert.True(rule > 0, "The sm collapse writes no geometry of its own.");

        // Read from the rule outwards rather than from the query inwards: the file carries
        // several 599.98px blocks (the time grid's, the emphasis ladder's) and the FIRST one is
        // not this one. The gate is whichever query opened last above the rule.
        Assert.StartsWith(
            "@container (max-width: 599.98px) {",
            css[css.LastIndexOf("@container", rule, StringComparison.Ordinal)..]);

        // Spelled line by line rather than as one block, so the assertion says what the rule is
        // and not how far the file happens to indent it.
        Assert.Contains(
            string.Join(
                '\n',
                "    .ns-collapse-sm.mud-button {",
                "        min-width: 0;",
                "        padding: 12px;",
                "        flex-shrink: 0;",
                "    }"),
            css);
    }

    /// <summary>A worded control is untouched by any of this: the rectangle is still what a
    /// label rides in, and the collapse hook is still what gives it up on a narrow
    /// container.</summary>
    [Fact]
    public void AWordedControlIsStillARectangleWithItsCollapseHook()
    {
        var cut = Render<NsButton>(p => p
            .Add(x => x.Icon, NsIcons.Edit)
            .Add(x => x.Label, "Editar"));

        var button = cut.Find("button");

        Assert.DoesNotContain("ns-square", button.ClassList);
        Assert.Contains("ns-collapse-sm", button.ClassList);
        Assert.Equal("Editar", button.QuerySelector(".ns-button-label")!.TextContent);
    }

    /// <summary>The one place the shape is written, read for what a render cannot see: no floor
    /// under the width (the vendor's is 64px, which is what made a lone glyph a pill) and the
    /// same padding on every side, which is what puts the box at the house's own icon action —
    /// 12 + 24 + 12. The corner radius is deliberately NOT here: .mud-button already gives the
    /// house's default, and restating it would be a second copy of one number.</summary>
    [Fact]
    public void TheSquareIsWrittenOnceAsTheHousesIconActionBox()
    {
        Assert.Contains(
            """
            .ns-square.mud-button {
                min-width: 0;
                padding: 12px;
                flex-shrink: 0;
            }
            """,
            ReadStylesheet());
    }

    /// <summary>Important and Default are one shape and one shadow with two washes (Leonardo,
    /// nsail#925: "filled disfrazado con accent, filled disfrazado con gray"), so a filled
    /// button is never flat. The wash used to cancel the vendor's filled shadow on both its
    /// states; that is what this reads for, in the rung's OWN two rules rather than over the
    /// whole file, so an unrelated box-shadow:none elsewhere cannot make it pass.</summary>
    // The window is the rung's own two rules and stops at the second one's brace: the next
    // selector down the file is a neighbour and not a boundary, and the blocks between them name
    // box-shadow for other rungs — the refused act's fill keeps the vendor's box-shadow:none —
    // which a wider window would read as this rung going flat.
    [Fact]
    public void TheSoftAccentKeepsTheFilledShadow()
    {
        var css = ReadStylesheet();
        var rung = css.IndexOf(".ns-important.mud-button-filled {", StringComparison.Ordinal);
        var hover = css.IndexOf(".ns-important.mud-button-filled:hover {", StringComparison.Ordinal);

        Assert.DoesNotContain("box-shadow", css[rung..css.IndexOf('}', hover)]);
    }

    IRenderedComponent<NsLink> RenderLink(NsAs rank, NsBreakpoint breakpoint)
    {
        return Render<NsLink>(p => p
            .Add(x => x.Href, "somewhere")
            .Add(x => x.As, rank)
            .Add(x => x.Breakpoint, breakpoint)
            .Add(x => x.Icon, NsIcons.Edit)
            // The flat rung's glyph pairs itself with a MudTooltip when it has a label, and a
            // tooltip is a popover the vendor will not render without a provider this harness
            // has none of. The label is dropped for that one case only; the class under test
            // is written from the breakpoint either way.
            .Add(x => x.Label, rank == NsAs.Inline ? null : "Editar"));
    }

    static IEnumerable<PropertyInfo> Parameters(Type control)
    {
        return control
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.IsDefined(typeof(ParameterAttribute), inherit: true));
    }

    static string ReadStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                // A Windows checkout writes CRLF into the working copy; the assertions spell LF.
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css")).ReplaceLineEndings("\n");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
