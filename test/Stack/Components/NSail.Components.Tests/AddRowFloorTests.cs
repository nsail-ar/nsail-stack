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

        var claim = Basis(css, ".ns-select-gutter")
            + Basis(css, ".ns-money-slot")
            + Token(css, "--ns-add-row-grow")
            + Token(css, "--ns-add-row-field")
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

        Assert.Contains("+ var(--ns-add-row-act) +", css, StringComparison.Ordinal);
    }

    // Keyed off the act itself rather than a class of the row's own: the act is what makes the
    // row wide enough to wrap, so no row can be given one without the floor that affords it.
    [Fact]
    public void TheFloorIsKeyedOffTheActTheRowCarries()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            """
            .ns-grid:has(> * > .ns-row-submit) {
                --ns-field-width: var(--ns-add-row-field);
                --ns-field-grow-basis: var(--ns-add-row-grow);
            }
            """,
            css,
            StringComparison.Ordinal);
    }

    /// <summary>The rem a custom property is declared at.</summary>
    static double Token(string css, string token)
    {
        return Read(css, $@"{Regex.Escape(token)}:\s*([\d.]+)rem;", token);
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
