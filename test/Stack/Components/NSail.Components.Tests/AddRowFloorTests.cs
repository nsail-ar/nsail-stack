// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text.RegularExpressions;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>An add row laid out beside a table (ui/styling.md, <em>Grid, stack, growth,
/// scroll</em>) wraps before anything in it shrinks, so what its items declare is what decides
/// whether it is a row at all — and its act dropping to a second line takes the last field off
/// the column the table reserved for it, which is the price axis a sheet exists for. So the sum
/// is the promise: the row's items have to declare less than the narrowest content the band
/// leaves them, and the act's cell and the column pinned under it have to be one declaration
/// rather than two numbers somebody keeps equal. bUnit lays out no box model and runs no media
/// query, so the arithmetic is read off the stylesheet the app ships and where the boxes actually
/// land is a browser walk's (Optical's PriceListGoldenPathTests).</summary>
public sealed class AddRowFloorTests
{
    // The grid's own gap is MudBlazor's gap-4 utility and not this stylesheet's, so it is the one
    // term read from outside: 1rem, four of them between five items.
    const double Gaps = 4;

    // The floor is the window NsTable stops drawing columns at — below it both halves of the
    // mirror are dropped and there is no column left to line up with — and the content the shell
    // leaves the grid there: 961px of window less the shell's own 312px, in rem.
    const double Floor = 649 / 16d;

    [Fact]
    public void TheRowsFiveItemsDeclareLessThanTheFloorLeavesThem()
    {
        var css = HouseStylesheet.Read();

        // The row's two open items — the article's Grow basis and the bounded picker's — read one
        // token between them, so the sum below is what the line is asked for whichever of the two
        // the surplus above the floor ends up in.
        var claim = Basis(css, ".ns-select-gutter")
            + Token(css, "--ns-money-slot")
            + (2 * Token(css, "--ns-add-row-field"))
            + Token(css, "--ns-add-row-act")
            + Gaps;

        Assert.True(
            claim <= Floor,
            $"the add row's items claim {claim}rem of the {Floor}rem the floor leaves them: at that "
            + "window the act wraps and the sheet's price axis parts ways with the row's own.");
    }

    // One declaration, read from both ends of the row: the act's cell takes its width from the
    // token and the table's last column reserves the same token, so the two cannot be moved
    // apart. A literal in either is the drift the mirror was built to stop.
    [Fact]
    public void TheActsCellAndTheColumnReservedForItReadOneToken()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            """
            .ns-grid > *:has(> .ns-row-submit) {
                --ns-field-width: var(--ns-add-row-act);
            }
            """,
            css,
            StringComparison.Ordinal);

        Assert.Contains(
            "width: calc(var(--ns-money-slot) + 1rem + var(--ns-add-row-act) + 1rem);",
            css,
            StringComparison.Ordinal);
    }

    // Keyed off the act itself rather than a class of the row's own: the act is what makes the
    // row wide enough to wrap, so no row can be given one without the floor that affords it. And
    // the floor is one figure declared on that grid — both of the row's open items read it from
    // there, the open-ended one through the family's own basis property and the bounded one by
    // inheritance — so nothing outside the row can be left spending a stale copy of it. A :root
    // token is what a figure the mirror's OTHER end has to read becomes, which is the act's cell
    // and only it.
    [Fact]
    public void TheFloorIsKeyedOffTheActTheRowCarriesAndDeclaredOnThatGrid()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            """
            .ns-grid:has(> * > .ns-row-submit) {
                --ns-add-row-field: 7rem;
                --ns-field-grow-basis: var(--ns-add-row-field);
            }
            """,
            css,
            StringComparison.Ordinal);

        Assert.Contains(
            """
            :root {
                --ns-add-row-act: 9rem;
            }
            """,
            css,
            StringComparison.Ordinal);
    }

    // The floor is what the WRAP is decided on, and a flex line is seated on the BASIS: so the
    // row spends it there and a bounded item grows back to the width it declares for itself out
    // of whatever the line has over the floor. Declared as a width instead, it would narrow the
    // item at every window above the band too — where the row already fitted and the narrowing
    // buys nothing but an unreadable field (Vigía, nsail#2127: a unit picker at 7rem showing the
    // tail of "Esf -1.00 / Cil -0.75" at 1600px of window).
    [Fact]
    public void TheFloorIsTheBasisTheLineIsSeatedOnAndNeverTheItemsWidth()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            """
            .ns-grid:has(> * > .ns-row-submit) > .ns-form-field:not(:has(> .ns-row-submit)) {
                flex: 1 1 var(--ns-add-row-field);
                max-width: var(--ns-field-width, 14rem);
            }
            """,
            css,
            StringComparison.Ordinal);

        Assert.DoesNotContain("--ns-field-width", Row(css), StringComparison.Ordinal);
    }

    // And released below the window the mirror itself is dropped at — the same boundary
    // .ns-select-gutter is hidden at and the reserve goes back to auto. There the row is a stack
    // of lines with no column to line up with, so a width answering a desktop band's arithmetic
    // would only be narrowing a phone's fields for nothing. Each of the three releases exactly
    // what its own selector declares above: the grid the basis, the act's cell the width, the
    // bounded item the family's own flex. A release of a property its selector never declares
    // reads as a rule that is still there.
    [Fact]
    public void TheFloorIsReleasedBelowTheWindowTheMirrorIsDroppedAt()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            """
            @media (max-width: 960px) {
                .ns-grid:has(> * > .ns-row-submit) {
                    --ns-field-grow-basis: initial;
                }

                .ns-grid > *:has(> .ns-row-submit) {
                    --ns-field-width: initial;
                }

                .ns-grid:has(> * > .ns-row-submit) > .ns-form-field:not(:has(> .ns-row-submit)) {
                    flex: 0 1 auto;
                    max-width: 100%;
                }
            }
            """,
            css,
            StringComparison.Ordinal);
    }

    /// <summary>The rem a token is declared at, wherever the sheet declares it.</summary>
    static double Token(string css, string token)
    {
        return Read(css, $@"{Regex.Escape(token)}:\s*([\d.]+)rem;", token);
    }

    /// <summary>What the add row's own grid hands the field family — the block keyed off the act
    /// the row carries, and nothing the cells inside it declare for themselves.</summary>
    static string Row(string css)
    {
        var found = Regex.Match(
            css,
            @"\.ns-grid:has\(> \* > \.ns-row-submit\) \{(?<body>[^}]*)\}",
            RegexOptions.Singleline);

        Assert.True(found.Success, "the stylesheet keys no floor off the act the add row carries.");

        return found.Groups["body"].Value;
    }

    /// <summary>The rem a flex shorthand declares as its basis — a bare item's own width, which
    /// is where the gutter and the price slot keep theirs.</summary>
    static double Basis(string css, string selector)
    {
        return Read(css, $@"{Regex.Escape(selector)}\s*\{{[^}}]*?flex:\s*\d+\s+\d+\s+([\d.]+)rem;", selector);
    }

    static double Read(string css, string pattern, string what)
    {
        var found = Regex.Match(css, pattern, RegexOptions.Singleline);

        Assert.True(found.Success, $"the stylesheet carries no rem width for {what}.");

        return double.Parse(found.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
