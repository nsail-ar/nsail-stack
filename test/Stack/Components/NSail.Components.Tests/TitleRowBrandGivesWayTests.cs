// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>The title row's copy of the brand is what gives way to the page's name: it shrinks
/// first and to nothing, its name elided on one line. Read off the stylesheet, which is where the
/// whole behaviour lives — bUnit lays out no box model, so a render proves the wrapper's class
/// and not what the class does. The geometry at a phone's width is a browser walk's. Each block
/// is quoted from column zero, so a block moved inside an at-rule no longer matches.</summary>
public sealed class TitleRowBrandGivesWayTests
{
    static readonly string Sheet = HouseStylesheet.Read().ReplaceLineEndings("\n");

    // The leading box is held to its row, or the brand would have nothing to give way to.
    [Fact]
    public void TheLeadingBoxIsHeldToItsRow()
    {
        AssertBlock(
            """
            .ns-title-lead {
                max-width: 100%;
            }
            """);
    }

    // A thousand to the title's one is what makes the brand yield before the title wraps a word;
    // the zero floor is what lets it yield all the way.
    [Fact]
    public void TheBrandShrinksFirstAndToNothing()
    {
        AssertBlock(
            """
            .ns-title-brand {
                flex: 0 1000 auto;
                min-width: 0;
                overflow: hidden;
            }
            """);
    }

    // The track asks for nothing at min-content, or a name that cannot wrap widens the page
    // past the viewport; the gap to the title sits inside the clip, so a gone brand leaves none.
    [Fact]
    public void TheBrandsFitAsksForNothingAtMinContent()
    {
        AssertBlock(
            """
            .ns-title-brand-fit {
                display: grid;
                grid-template-columns: minmax(0, max-content);
                min-width: 0;
                margin-inline-end: 0.75rem;
            }
            """);
    }

    [Fact]
    public void TheBrandsNameIsElidedOnOneLine()
    {
        AssertBlock(
            """
            .ns-title-brand .d-flex {
                min-width: 0;
            }
            """);

        AssertBlock(
            """
            .ns-title-brand .mud-typography {
                overflow: hidden;
                white-space: nowrap;
                text-overflow: ellipsis;
            }
            """);
    }

    static void AssertBlock(string block)
    {
        Assert.Contains("\n" + block.ReplaceLineEndings("\n") + "\n", Sheet);
    }
}
