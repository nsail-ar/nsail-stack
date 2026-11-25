// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Components.Tests;

/// <summary>nsail#4332 — a wordless chromed face and a worded one in the same row are one height
/// under either pointer, so a toolbar that mixes them stands straight. Read off the stylesheet the
/// way TouchTargetFloorTests reads the floor: bUnit lays out no box. What the row then measures on
/// a real screen is the browser walks'.</summary>
public sealed partial class SquareRowHeightTests
{
    // The worded face's block padding: the vendor's medium .mud-button (6px 16px), which the house
    // leaves standing under a fine pointer. A square that wrote any other number here would stand
    // taller or shorter than the label beside it.
    const string WordedBlockPadding = "6px";

    // The box a label rides in is one line of the button's own text.
    const string OneLine = "1lh";

    // The vendor's icon-button padding and the glyph it pads (.mud-button-icon-size-medium, 20px),
    // which under a coarse pointer has to come out at the floor TouchTargetFloorTests pins.
    const int IconButtonPadding = 12;
    const int MdGlyph = 20;
    const int Floor = 44;

    const string FooterAct = ".ns-panel-footer .mud-button-filled:has(.mud-button-icon-start):not(.mud-button-filled-primary):not(.mud-button-filled-error)";

    // Each face a container query takes the word from, and the glyph wrapper inside it.
    static readonly (string Face, string Glyph)[] Collapses =
    [
        (".ns-collapse-sm.mud-button", ".ns-collapse-sm .mud-button-icon-start"),
        (".ns-collapse-md.mud-button", ".ns-collapse-md .mud-button-icon-start"),
        (".ns-collapse-lg.mud-button", ".ns-collapse-lg .mud-button-icon-start"),
        (".ns-collapse-xl.mud-button", ".ns-collapse-xl .mud-button-icon-start"),
        (FooterAct, FooterAct + " .mud-button-icon-start"),
    ];

    [Fact]
    public void Under_a_fine_pointer_the_square_is_the_worded_faces_padding_around_one_line()
    {
        var css = Rules(ReadStylesheet());

        var root = Assert.Single(css, rule => rule.Selector == ":root" && rule.Query is null && rule.Has("--ns-square-padding"));

        Assert.Equal(WordedBlockPadding, root.Get("--ns-square-padding"));
        Assert.Equal(OneLine, root.Get("--ns-square-glyph"));
    }

    [Fact]
    public void Under_a_coarse_pointer_the_square_is_the_floor_by_construction()
    {
        var css = Rules(ReadStylesheet());

        var root = Assert.Single(css, rule => rule.Selector == ":root" && rule.Query == "@media (pointer: coarse)" && rule.Has("--ns-square-padding"));

        Assert.Equal($"{IconButtonPadding}px", root.Get("--ns-square-padding"));
        Assert.Equal("auto", root.Get("--ns-square-glyph"));
        Assert.Equal(Floor, IconButtonPadding + MdGlyph + IconButtonPadding);
    }

    /// <summary>K2: equal padding on every side, no floor under the width, no shrinking in a
    /// flex row, and a glyph box as wide as it is tall — the square is a square at both sizes.</summary>
    [Fact]
    public void The_square_reads_both_sizes_and_stays_a_square()
    {
        var css = Rules(ReadStylesheet());

        var face = Assert.Single(css, rule => rule.Selector == ".ns-square.mud-button");

        Assert.Equal("0", face.Get("min-width"));
        Assert.Equal("var(--ns-square-padding)", face.Get("padding"));
        Assert.Equal("0", face.Get("flex-shrink"));

        AssertSquareGlyph(Assert.Single(css, rule => rule.Selector == ".ns-square .mud-button-icon-start"));
    }

    /// <summary>AC2: a face whose word the container took away reads the same pair, so it is the
    /// square's height and not a second one of its own.</summary>
    [Fact]
    public void Every_collapsed_face_takes_the_squares_geometry()
    {
        var css = Rules(ReadStylesheet());

        foreach (var (face, glyph) in Collapses)
        {
            var rule = Assert.Single(css, rule => rule.Selector == face);

            Assert.StartsWith("@container ", rule.Query);
            Assert.Equal("0", rule.Get("min-width"));
            Assert.Equal("var(--ns-square-padding)", rule.Get("padding"));

            AssertSquareGlyph(Assert.Single(css, rule => rule.Selector == glyph));
        }
    }

    /// <summary>Nothing else in the file gives the square's padding a number: a page or a
    /// container that did would be the per-caller branch principles.md refuses.</summary>
    [Fact]
    public void The_pair_is_declared_once_for_each_pointer()
    {
        var css = Rules(ReadStylesheet());

        Assert.Equal(2, css.Count(rule => rule.Has("--ns-square-padding")));
        Assert.Equal(2, css.Count(rule => rule.Has("--ns-square-glyph")));
    }

    static void AssertSquareGlyph(Rule glyph)
    {
        Assert.Equal("0", glyph.Get("margin-inline"));
        Assert.Equal("var(--ns-square-glyph)", glyph.Get("inline-size"));
        Assert.Equal("var(--ns-square-glyph)", glyph.Get("block-size"));
    }

    sealed record Rule(string Selector, string? Query, IReadOnlyDictionary<string, string> Declarations)
    {
        public bool Has(string property)
        {
            return Declarations.ContainsKey(property);
        }

        public string Get(string property)
        {
            Assert.True(Declarations.TryGetValue(property, out var value), $"'{Selector}' declares no {property}.");

            return value;
        }
    }

    /// <summary>Every style rule in the file with the at-rule it sits in, if any. Comments are
    /// stripped first: a brace inside one is prose. A property a rule repeats (a fallback) reads
    /// as its last value, the one the cascade keeps.</summary>
    static List<Rule> Rules(string css)
    {
        css = Comment().Replace(css, "");

        var rules = new List<Rule>();
        var open = new Stack<string>();
        var start = 0;

        for (var at = 0; at < css.Length; at++)
        {
            if (css[at] == '{')
            {
                open.Push(css[start..at].Trim());
                start = at + 1;
            }
            else if (css[at] == '}')
            {
                var head = open.Pop();
                var body = css[start..at];

                if (!head.StartsWith('@') && body.Contains(':'))
                {
                    var query = open.FirstOrDefault(outer => outer.StartsWith('@'));
                    var declarations = body
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(line => line.Split(':', 2))
                        .Where(pair => pair.Length == 2)
                        .GroupBy(pair => pair[0].Trim())
                        .ToDictionary(group => group.Key, group => group.Last()[1].Trim());

                    rules.Add(new Rule(head, query, declarations));
                }

                start = at + 1;
            }
        }

        return rules;
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

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
