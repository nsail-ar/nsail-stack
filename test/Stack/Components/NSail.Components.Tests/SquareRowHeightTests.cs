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
    // Each filled size's selector, the worded face's block padding at that size (the vendor's
    // filled button: 4px 10px, 6px 16px, 8px 22px), which the house leaves standing under a fine
    // pointer, and the glyph the vendor sets in a button of that size (.mud-button-icon-size-*,
    // which outranks the house's .mud-icon-size-* step on a button's own icon). A square that
    // padded any other number under a fine pointer would stand taller or shorter than the label
    // beside it.
    static readonly (string Selector, int WordedBlockPadding, int Glyph)[] Sizes =
    [
        (".mud-button-filled-size-small", 4, 18),
        (".mud-button-filled", 6, 20),
        (".mud-button-filled-size-large", 8, 22),
    ];

    public static TheoryData<string, int> WordedPaddings()
    {
        var data = new TheoryData<string, int>();

        foreach (var size in Sizes)
        {
            data.Add(size.Selector, size.WordedBlockPadding);
        }

        return data;
    }

    public static TheoryData<string, int> Glyphs()
    {
        var data = new TheoryData<string, int>();

        foreach (var size in Sizes)
        {
            data.Add(size.Selector, size.Glyph);
        }

        return data;
    }

    // The box a label rides in is one line of the button's own text.
    const string OneLine = "1lh";

    // Under a coarse pointer each square has to come out at the floor TouchTargetFloorTests pins.
    const int Floor = 44;

    const string FooterAct = ".ns-panel-footer .mud-button-filled:has(.mud-button-icon-start):not(.mud-button-filled-primary):not(.mud-button-filled-error)";

    // Each face a container query takes the word from, and the glyph wrapper inside it.
    static readonly (string Face, string Glyph)[] Collapses =
    [
        (".ns-collapse-sm.mud-button", ".ns-collapse-sm .mud-button-label .mud-button-icon-start"),
        (".ns-collapse-md.mud-button", ".ns-collapse-md .mud-button-label .mud-button-icon-start"),
        (".ns-collapse-lg.mud-button", ".ns-collapse-lg .mud-button-label .mud-button-icon-start"),
        (".ns-collapse-xl.mud-button", ".ns-collapse-xl .mud-button-label .mud-button-icon-start"),
        (FooterAct, FooterAct + " .mud-button-icon-start"),
    ];

    [Theory]
    [MemberData(nameof(WordedPaddings))]
    public void Under_a_fine_pointer_the_square_is_the_worded_faces_padding_around_one_line(string size, int wordedBlockPadding)
    {
        var css = Rules(ReadStylesheet());

        var face = Assert.Single(css, rule => rule.Selector == size && rule.Query is null && rule.Has("--ns-square-padding"));

        Assert.Equal($"{wordedBlockPadding}px", face.Get("--ns-square-padding"));
        Assert.Equal(OneLine, Assert.Single(css, rule => rule.Selector == ".mud-button-filled" && rule.Query is null).Get("--ns-square-glyph"));
    }

    [Theory]
    [MemberData(nameof(Glyphs))]
    public void Under_a_coarse_pointer_the_square_is_the_floor_by_construction(string size, int glyph)
    {
        var css = Rules(ReadStylesheet());

        var face = Assert.Single(css, rule => rule.Selector == size && rule.Query == "@media (pointer: coarse)" && rule.Has("--ns-square-padding"));
        var padding = int.Parse(face.Get("--ns-square-padding").TrimEnd('p', 'x'));

        Assert.Equal(Floor, padding + glyph + padding);
        Assert.Equal("auto", Assert.Single(css, rule => rule.Selector == ".mud-button-filled" && rule.Query == "@media (pointer: coarse)").Get("--ns-square-glyph"));
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

        AssertSquareGlyph(Assert.Single(css, rule => rule.Selector == ".ns-square .mud-button-label .mud-button-icon-start"));
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

    /// <summary>A collapsed face with no fill (NsButton's Inline rung) reads the pair too, so every
    /// boxed face carries one: the flat glyph's box, 12px around the Md glyph at either pointer,
    /// declared before the filled face that overrides it. An undefined property would pad it 0 —
    /// a 20px target, grown by the floor to a 20 by 44 rectangle.</summary>
    [Fact]
    public void A_face_with_no_fill_collapses_to_the_flat_glyphs_box()
    {
        var css = Rules(ReadStylesheet());

        var face = Assert.Single(css, rule => rule.Selector == ".mud-button" && rule.Has("--ns-square-padding"));

        Assert.Null(face.Query);
        Assert.Equal("12px", face.Get("--ns-square-padding"));
        Assert.Equal("auto", face.Get("--ns-square-glyph"));
        Assert.True(css.IndexOf(face) < css.FindIndex(rule => rule.Selector == ".mud-button-filled" && rule.Has("--ns-square-padding")),
            "The default has to come before the filled face's pair, at the same weight, so the fill wins.");
    }

    /// <summary>Nothing but the boxed face's default and the filled face's own size classes gives
    /// the square's padding a number, once per pointer: a page or a container that did would be
    /// the per-caller branch principles.md refuses.</summary>
    [Fact]
    public void The_pair_is_declared_once_for_each_pointer_and_size()
    {
        var css = Rules(ReadStylesheet());
        var sizes = Sizes.Select(size => size.Selector).ToHashSet();

        var padding = css.Where(rule => rule.Has("--ns-square-padding") && rule.Selector != ".mud-button").ToList();
        var glyph = css.Where(rule => rule.Has("--ns-square-glyph") && rule.Selector != ".mud-button").ToList();

        Assert.Equal(2 * sizes.Count, padding.Count);
        Assert.All(padding, rule => Assert.Contains(rule.Selector, sizes));
        Assert.Equal(2, glyph.Count);
        Assert.All(glyph, rule => Assert.Equal(".mud-button-filled", rule.Selector));
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
