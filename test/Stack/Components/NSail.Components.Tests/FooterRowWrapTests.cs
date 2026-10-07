// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>A footer act never gives up its content width: where the acts do not fit the row
/// grows a line, the same answer the title row and the toolbar row above it give. A row that
/// cannot wrap has only flex-shrink left, and a shrunk worded button breaks its own LABEL in
/// two. Read off the stylesheet, which is where the whole behaviour lives — bUnit lays out no
/// box model and runs no container query (FooterButtonCollapseTests says so for the ladder in
/// the same block), so what a render could prove is the DOM shape and not the geometry. What
/// the rule then does to five worded acts at a real width is a browser walk's.</summary>
public sealed class FooterRowWrapTests
{
    const string Footer = ".ns-panel-footer {";
    const string ActGroup = ".ns-panel-footer .ns-footer-action-row {";
    const string Wrap = "flex-wrap: wrap;";
    const string Phone = "@container (max-width: 599.98px) {";

    // One declaration is the whole behaviour, so a second copy inside the phone query would be
    // a value stated twice and a width at which the row's answer could drift from the rule.
    [Fact]
    public void TheRowWrapsAtEveryWidth_StatedOnceOutsideThePhoneQuery()
    {
        var lines = Read();

        var block = Assert.Single(Blocks(lines, Footer));

        Assert.Equal([Wrap], block.Declarations);
        Assert.Null(block.Query);
    }

    // The group is an NsStack, and MudBlazor's own .flex-nowrap utility carries !important:
    // nothing but another !important reaches over it, which is why this one declaration is the
    // only place the house writes the group's wrap.
    [Fact]
    public void TheGroupWrapsInsideItself_OverTheVendorsOwnNoWrap()
    {
        var lines = Read();

        var block = Assert.Single(Blocks(lines, ActGroup));

        Assert.Equal(["flex-wrap: wrap !important;"], block.Declarations);
        Assert.Null(block.Query);
    }

    // What the phone still owes, unchanged: the ladder's collapse, the refusal's own line above
    // the buttons, and the act group's unconditional full-width split under the figure.
    [Fact]
    public void ThePhoneQueryKeepsTheLadderAndTheTwoLinesItSplits()
    {
        var lines = Read();

        var seated = Assert.Single(Blocks(lines, ".ns-panel-footer .ns-form-problem-inline {"));

        Assert.Equal(["flex-basis: 100%;", "order: -1;"], seated.Declarations);
        Assert.Equal(Phone, seated.Query);

        var split = Assert.Single(Blocks(lines, ".ns-footer-action-row {"));

        Assert.Equal(["flex-basis: 100%;"], split.Declarations);
        Assert.Equal(Phone, split.Query);

        // The ladder itself, whose own collapse is the phone's other concession to that width.
        Assert.All(
            Blocks(lines, ".ns-panel-footer .mud-button-filled:has(.mud-button-icon-start):not(.mud-button-filled-primary):not(.mud-button-filled-error) .ns-button-label {"),
            block => Assert.Equal(Phone, block.Query));
    }

    // The notice is the one child that gives, and a flex basis of zero rather than its sentence
    // is what that costs: a wrap row breaks at a child's flex base size, so a notice measured by
    // its text is what would push an act onto another line.
    [Fact]
    public void TheSeatedRefusalDeclaresNoWidthOfItsOwn()
    {
        var lines = Read();

        var block = Assert.Single(Blocks(lines, ".ns-form-problem-inline {"));

        Assert.Contains("flex: 1 1 0;", block.Declarations);
        Assert.Contains("min-width: 0;", block.Declarations);
        Assert.Null(block.Query);
    }

    /// <summary>Every block the stylesheet opens with <paramref name="selector"/>: its own
    /// declarations, and the at-rule it is nested in or null where it is not nested at all.
    /// Fails loudly on a stylesheet that no longer carries the selector rather than passing on
    /// its absence.</summary>
    static Block[] Blocks(string[] lines, string selector)
    {
        var blocks = new List<Block>();
        var queries = new Stack<string>();

        for (var walk = 0; walk < lines.Length; walk++)
        {
            if (lines[walk] == "}")
            {
                if (queries.Count > 0)
                {
                    queries.Pop();
                }

                continue;
            }

            if (!lines[walk].EndsWith('{'))
            {
                continue;
            }

            if (lines[walk] != selector)
            {
                queries.Push(lines[walk]);

                continue;
            }

            var declarations = new List<string>();

            for (var read = walk + 1; lines[read] != "}"; read++)
            {
                if (lines[read].EndsWith(';'))
                {
                    declarations.Add(lines[read]);
                }
            }

            blocks.Add(new Block(queries.Count == 0 ? null : queries.Peek(), [.. declarations]));
        }

        Assert.NotEmpty(blocks);

        return [.. blocks];
    }

    // Comments are dropped so a declaration quoted inside one is never read as a rule, and the
    // blank lines with them so a block's own declarations read as a list.
    static string[] Read()
    {
        var lines = new List<string>();
        var commented = false;

        foreach (var line in HouseStylesheet.Read().Split('\n').Select(line => line.Trim()))
        {
            if (commented)
            {
                commented = !line.EndsWith("*/", StringComparison.Ordinal);

                continue;
            }

            if (line.StartsWith("/*", StringComparison.Ordinal))
            {
                commented = !line.EndsWith("*/", StringComparison.Ordinal);

                continue;
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return [.. lines];
    }

    readonly record struct Block(string? Query, string[] Declarations);
}
